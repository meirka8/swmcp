using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwBridge;

namespace SeedVerifier;

public static partial class Zoo
{
    private const string ZooPath = @"C:\projects\aibuilds\models\FeatureZoo.SLDPRT";

    // Dimensions the zoo is built with. These are the ground truth the live
    // value-verification compares against â€” all SI (meters / radians).
    public const double BlockSize = 0.100;      // 100 mm square
    public const double BlockDepth = 0.050;     // 50 mm
    public const double BossDepth = 0.020;      // 20 mm
    public const double BossDraft = 0.0872664626;   // 5 deg
    public const double CutDepth = 0.015;       // 15 mm
    public const double FilletRadius = 0.006;   // 6 mm
    public const double ChamferDist = 0.004;    // 4 mm
    public const double ChamferAngle = 0.7853981634; // 45 deg
    public const double CirTotalAngle = 6.2831853072; // 360 deg
    public const int CirInstances = 4;
    public const int LinInstances = 3;
    public const double LinSpacing = 0.022;     // 22 mm
    public const double RevAngle = 1.5707963268;    // 90 deg
    public const double RevCutAngle = 3.1415926536; // 180 deg
    public const double ShellThickness = 0.003; // 3 mm
    public const double DomeHeight = 0.008;     // 8 mm
    public const double DraftAngle = 0.0523598776;  // 3 deg
    public const double PlaneOffset = 0.030;    // 30 mm

    private static readonly List<string> Log = new();
    private static void Say(string s) { Log.Add(s); Console.WriteLine(s); }

    public static int Run(string seedPath)
    {
        var connection = new SwConnection();
        var docs = new DocumentManager(connection);

        var before = docs.ListOpenDocuments().Select(d => d.Title).ToList();
        Say($"open documents BEFORE: {string.Join(", ", before)}");

        var sw = connection.GetApp();
        Say($"SolidWorks revision: {sw.RevisionNumber()}");

        var template = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
        Say($"part template: {template}");

        var doc = (ModelDoc2)sw.NewDocument(template, 0, 0, 0);
        if (doc == null) { Say("FATAL: NewDocument returned null"); return 1; }
        Say($"created scratch doc: {doc.GetTitle()}");
        CurrentDoc = doc;

        try
        {
            Build(sw, doc);

            doc.ForceRebuild3(false);
            int err = 0, warn = 0;
            var saved = doc.Extension.SaveAs(ZooPath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_OverrideSaveEmodel,
                null, ref err, ref warn);
            Say($"SaveAs '{ZooPath}': {saved} (err={err}, warn={warn})");

            Report(doc, seedPath);
        }
        catch (Exception ex)
        {
            Say($"FATAL during build: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            var title = doc.GetTitle();
            sw.CloseDoc(title);
            Say($"closed scratch doc '{title}'");
        }

        var after = docs.ListOpenDocuments().Select(d => d.Title).ToList();
        Say($"open documents AFTER: {string.Join(", ", after)}");
        Say(before.OrderBy(x => x).SequenceEqual(after.OrderBy(x => x))
            ? "OK: pre-existing documents untouched"
            : "WARN: open-document set changed");

        File.WriteAllText(
            @"C:\projects\aibuilds\swmcp\tests\SeedVerifier\last-zoo-run.log",
            string.Join(System.Environment.NewLine, Log), Encoding.UTF8);
        return 0;
    }

    // ---------------------------------------------------------------- build

    private static void Build(ISldWorks sw, ModelDoc2 doc)
    {
        var fm = doc.FeatureManager;
        var sk = doc.SketchManager;
        var half = BlockSize / 2;

        // Geometry is centred on the origin so that Right Plane / Top Plane cut
        // through the block — otherwise a mirror about them lands off the body
        // and the mirror feature errors out.

        // --- 1. base block -------------------------------------------------
        Step("Extrusion (base block)", () =>
        {
            SelectPlane(doc, "Front Plane");
            sk.InsertSketch(true);
            sk.CreateCornerRectangle(-half, -half, 0, half, half, 0);
            sk.InsertSketch(true);
            return fm.FeatureExtrusion3(true, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0, BlockDepth, 0,
                false, false, false, false, 0, 0,
                false, false, false, false, true, true, true,
                (int)swStartConditions_e.swStartSketchPlane, 0, false);
        });

        // Learn which way the block went, so later start-offsets follow it.
        var box = BodyBox(doc);
        var zTop = box == null ? BlockDepth : Math.Max(Math.Abs(box[2]), Math.Abs(box[5]));
        Say($"    block body box: [{string.Join(", ", (box ?? Array.Empty<double>()).Select(v => v.ToString("F4")))}]");

        // --- 1b. cut, while the block is still solid ------------------------
        // The cut has to precede the shell: once the block is hollow a blind
        // cut from the bottom sketch plane meets the cavity and SolidWorks
        // silently returns no feature.
        Step("Cut", () =>
        {
            SelectPlane(doc, "Front Plane");
            sk.InsertSketch(true);
            sk.CreateCircleByRadius(0.03, 0, 0, 0.007);
            sk.InsertSketch(true);
            var sketchFeature = LastRealFeature(doc);
            Say($"    cut sketch feature: '{sketchFeature?.Name}' ({sketchFeature?.GetTypeName2()})");

            Feature? Attempt(string label, Func<Feature?> call)
            {
                doc.ClearSelection2(true);
                var selected = sketchFeature?.Select2(false, 0) ?? false;
                Feature? r = null;
                try { r = call(); }
                catch (Exception ex) { Say($"    cut variant {label}: threw {ex.GetType().Name}: {ex.Message}"); }
                Say($"    cut variant {label}: sketchSelected={selected} -> {(r == null ? "null" : r.Name)}");
                return r;
            }

            return Attempt("blind/fwd", () => fm.FeatureCut4(true, false, false,
                       (int)swEndConditions_e.swEndCondBlind, 0, CutDepth, 0,
                       false, false, false, false, 0, 0,
                       false, false, false, false, false, true, true,
                       false, false, false,
                       (int)swStartConditions_e.swStartSketchPlane, 0, false, false))
                ?? Attempt("blind/rev", () => fm.FeatureCut4(true, false, true,
                       (int)swEndConditions_e.swEndCondBlind, 0, CutDepth, 0,
                       false, false, false, false, 0, 0,
                       false, false, false, false, false, true, true,
                       false, false, false,
                       (int)swStartConditions_e.swStartSketchPlane, 0, false, false));
        });

        // --- 2. straight cylindrical boss at the centre (gives us an axis) --
        Step("Extrusion (centre cylinder, start-offset from sketch plane)", () =>
        {
            SelectPlane(doc, "Front Plane");
            sk.InsertSketch(true);
            sk.CreateCircleByRadius(0, 0, 0, 0.012);
            sk.InsertSketch(true);
            return fm.FeatureExtrusion3(true, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0, BossDepth, 0,
                false, false, false, false, 0, 0,
                false, false, false, false, true, true, true,
                (int)swStartConditions_e.swStartOffset, zTop, false);
        });

        // --- 3. drafted boss (verifies GetDraftAngle non-zero) --------------
        Step("Extrusion (drafted boss)", () =>
        {
            SelectPlane(doc, "Front Plane");
            sk.InsertSketch(true);
            sk.CreateCircleByRadius(-0.03, -0.03, 0, 0.008);
            sk.InsertSketch(true);
            return fm.FeatureExtrusion3(true, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0, BossDepth, 0,
                true, false, false, false, BossDraft, 0,
                false, false, false, false, true, true, true,
                (int)swStartConditions_e.swStartOffset, zTop, false);
        });

        // --- 5. fillet on one vertical edge --------------------------------
        Step("Fillet", () =>
        {
            var edge = PickVerticalBlockEdge(doc, -half, half);
            if (edge == null) { Say("    no vertical edge found at (-half,+half)"); return null; }
            doc.ClearSelection2(true);
            SelectEntity(doc, edge, 0);
            // 195 = Propagate|UniformRadius|AttachEdges|KeepFeatures — the
            // combination the SolidWorks API examples use for a simple fillet.
            return fm.FeatureFillet3(195, FilletRadius, 0, 0,
                (int)swFeatureFilletType_e.swFeatureFilletType_Simple, 0, 0,
                null, null, null, null, null, null, null) as Feature;
        });

        // --- 6. chamfer on another vertical edge ---------------------------
        Step("Chamfer", () =>
        {
            var edge = PickVerticalBlockEdge(doc, half, -half);
            if (edge == null) { Say("    no vertical edge found at (+half,-half)"); return null; }
            doc.ClearSelection2(true);
            SelectEntity(doc, edge, 1);
            return fm.InsertFeatureChamfer((int)swFeatureChamferOption_e.swFeatureChamferTangentPropagation,
                (int)swChamferType_e.swChamferAngleDistance,
                ChamferDist, ChamferAngle, 0, 0, 0, 0);
        });

        // --- 7. reference axis through the centre cylinder -----------------
        Step("RefAxis (support for CirPattern)", () =>
        {
            var face = PickCylindricalFace(doc);
            if (face == null) { Say("    no cylindrical face found"); return null; }
            doc.ClearSelection2(true);
            SelectEntity(doc, face, 0);
            var ok = doc.InsertAxis2(true);
            Say($"    InsertAxis2 -> {ok}");
            return NewFeatureSince(doc, _snapshot);
        });

        // --- 7b. extra boss, the linear pattern's seed ----------------------
        Step("Extrusion (LPattern seed boss)", () =>
        {
            SelectPlane(doc, "Front Plane");
            sk.InsertSketch(true);
            sk.CreateCircleByRadius(-0.03, 0.02, 0, 0.006);
            sk.InsertSketch(true);
            return fm.FeatureExtrusion3(true, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0, BossDepth, 0,
                false, false, false, false, 0, 0,
                false, false, false, false, true, true, true,
                (int)swStartConditions_e.swStartOffset, zTop, false);
        });

        // --- 8. circular pattern -------------------------------------------
        Step("CirPattern", () =>
        {
            doc.ClearSelection2(true);
            if (!SelectByName(doc, "Axis1", "AXIS", 1)) { Say("    Axis1 not selectable"); return null; }
            var seed = FeatureNames(doc).Contains("Cut-Extrude1") ? "Cut-Extrude1" : "Boss-Extrude3";
            if (!SelectByName(doc, seed, "BODYFEATURE", 4)) { Say($"    {seed} not selectable"); return null; }
            Say($"    pattern seed: {seed}");
            return fm.FeatureCircularPattern4(CirInstances, CirTotalAngle, false, "NULL", false, true, false);
        });

        // --- 9. linear pattern ----------------------------------------------
        Step("LPattern", () =>
        {
            var edge = PickHorizontalBlockEdge(doc);
            if (edge == null) { Say("    no horizontal edge found"); return null; }
            doc.ClearSelection2(true);
            SelectEntity(doc, edge, 1);
            if (!SelectByName(doc, "Boss-Extrude4", "BODYFEATURE", 4)) { Say("    Boss-Extrude4 not selectable"); return null; }
            return fm.FeatureLinearPattern2(LinInstances, LinSpacing, 1, 0, false, false, "NULL", "NULL", false);
        });

        // --- 10. mirror ------------------------------------------------------
        Step("MirrorPattern", () =>
        {
            Feature? Try(int planeMark, int featMark, string feat)
            {
                doc.ClearSelection2(true);
                var p = SelectByName(doc, "Right Plane", "PLANE", planeMark);
                var g = SelectByName(doc, feat, "BODYFEATURE", featMark);
                Feature? r = null;
                try { r = fm.InsertMirrorFeature2(false, false, false, false, 0); }
                catch (Exception ex) { Say($"    mirror(plane={planeMark},feat={featMark},{feat}) threw {ex.Message}"); }
                Say($"    mirror(plane={planeMark},feat={featMark},{feat}) planeSel={p} featSel={g} -> {(r == null ? "null" : r.Name)}");
                return r;
            }
            return Try(2, 1, "Boss-Extrude3") ?? Try(1, 2, "Boss-Extrude3") ?? Try(2, 1, "Chamfer1");
        });

        // --- 11. reference plane ---------------------------------------------
        Step("RefPlane", () =>
        {
            doc.ClearSelection2(true);
            if (!SelectByName(doc, "Front Plane", "PLANE", 0)) { Say("    Front Plane not selectable"); return null; }
            return fm.InsertRefPlane((int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance,
                PlaneOffset, 0, 0, 0, 0) as Feature;
        });

        // --- 12. revolve (separate body, well clear of the block) ------------
        Step("Revolution", () =>
        {
            SelectPlane(doc, "Right Plane");
            sk.InsertSketch(true);
            var axis = sk.CreateCenterLine(0.200, 0, 0, 0.200, 0.050, 0);
            sk.CreateCornerRectangle(0.210, 0.000, 0, 0.220, 0.030, 0);
            sk.InsertSketch(true);
            var sketchFeature = LastRealFeature(doc);
            doc.ClearSelection2(true);
            axis.Select4(false, MakeSelData(doc, 4));
            sketchFeature?.Select2(true, 0);
            return fm.FeatureRevolve2(true, true, false, false, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0, RevAngle, 0,
                false, false, 0, 0, 0, 0, 0, false, false, true);
        });

        // --- 12. revolve cut into that body ----------------------------------
        Step("RevCut", () =>
        {
            SelectPlane(doc, "Right Plane");
            sk.InsertSketch(true);
            var axis = sk.CreateCenterLine(0.200, 0, 0, 0.200, 0.050, 0);
            sk.CreateCornerRectangle(0.212, 0.005, 0, 0.218, 0.012, 0);
            sk.InsertSketch(true);
            var sketchFeature = LastRealFeature(doc);
            doc.ClearSelection2(true);
            axis.Select4(false, MakeSelData(doc, 4));
            sketchFeature?.Select2(true, 0);
            return fm.FeatureRevolveCut2(RevCutAngle, false, 0,
                (int)swRevolveType_e.swRevolveTypeOneDirection, 0, false, true, false, false, false);
        });

        // --- 13. draft on the block's side faces ------------------------------
        Step("Draft", () =>
        {
            // The shell removes the block's bottom face, so fall back to the top.
            var neutral = PickPlanarFaceByNormal(doc, 0, 0, -1) ?? PickPlanarFaceByNormal(doc, 0, 0, 1);
            var target = PickPlanarFaceByNormal(doc, 0, -1, 0);
            Say($"    draft neutral={(neutral == null ? "NULL" : "ok")} target={(target == null ? "NULL" : "ok")}");
            if (neutral == null || target == null) { Say("    could not find neutral/target faces"); return null; }
            doc.ClearSelection2(true);
            SelectEntity(doc, neutral, 1);
            SelectEntity(doc, target, 2, append: true);
            return fm.InsertMultiFaceDraft(DraftAngle, false, false,
                0 /*no propagation*/, false, false);
        });

        // --- 14. control: does a NON-base, separate-body extrude report
        //         "Extrusion" or "ICE"? -------------------------------------
        Step("Extrusion (separate body, Merge=false — ICE control)", () =>
        {
            SelectPlane(doc, "Front Plane");
            sk.InsertSketch(true);
            sk.CreateCircleByRadius(0.300, 0, 0, 0.010);
            sk.InsertSketch(true);
            return fm.FeatureExtrusion3(true, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0, BossDepth, 0,
                false, false, false, false, 0, 0,
                false, false, false, false, false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0, false);
        });

        // --- 16. a plain scratch box, well clear of everything else, to host
        //         the two features that will not survive a busy body ---------
        Step("Extrusion (scratch box for Shell/Dome)", () =>
        {
            SelectPlane(doc, "Front Plane");
            sk.InsertSketch(true);
            sk.CreateCornerRectangle(0.150, -0.020, 0, 0.190, 0.020, 0);
            sk.InsertSketch(true);
            return fm.FeatureExtrusion3(true, false, false,
                (int)swEndConditions_e.swEndCondBlind, 0, 0.030, 0,
                false, false, false, false, 0, 0,
                false, false, false, false, false, false, false,
                (int)swStartConditions_e.swStartSketchPlane, 0, false);
        });

        // --- 17. shell that scratch box ---------------------------------------
        // Shelling the main block fails once it carries the blind cut (and again
        // once it carries the patterns) — SolidWorks just returns no feature.
        // A plain box always shells, and Thickness is what we need to read.
        Step("Shell", () =>
        {
            doc.ClearSelection2(true);
            var face = PickFaceInRegion(doc, 0, 0, -1, 0.14, 0.20);
            if (face == null) { Say("    no scratch-box bottom face found"); return null; }
            var sel = SelectEntity(doc, face, 0);
            Say($"    shell face selected={sel}");
            doc.InsertFeatureShell(ShellThickness, false);
            return NewFeatureSince(doc, _snapshot);
        });

        // --- 18. dome (last: flakiest) -----------------------------------------
        Step("Dome", () =>
        {
            doc.ClearSelection2(true);
            var candidates = new (string, Func<object?>)[]
            {
                ("scratch box top", () => PickFaceInRegion(doc, 0, 0, 1, 0.14, 0.20)),
                ("cylinder top", () => PickTopPlanarFace(doc)),
                ("block top", () => PickPlanarFaceByNormal(doc, 0, 0, 1)),
            };
            foreach (var (label, picker) in candidates)
            {
                var face = picker();
                if (face == null) { Say($"    dome: no face for '{label}'"); continue; }
                var b = ((Face2)face).GetBox() as double[];
                Say($"    dome try '{label}' box=[{string.Join(", ", (b ?? Array.Empty<double>()).Select(v => v.ToString("F4")))}] area={((Face2)face).GetArea():G6}");
                foreach (var rev in new[] { false, true })
                {
                    doc.ClearSelection2(true);
                    SelectEntity(doc, face, 0);
                    try { doc.InsertDome(DomeHeight, rev, false); }
                    catch (Exception ex) { Say($"      InsertDome(rev={rev}) threw {ex.Message}"); }
                    var f = NewFeatureSince(doc, _snapshot);
                    if (f != null) return f;
                    Say($"      InsertDome(rev={rev}) produced no feature");
                }
            }
            return null;
        });
    }

    // Feature names present when the current Step() began — used by the
    // void-returning creators (InsertDome/InsertFeatureShell/InsertAxis2) to
    // find what they actually produced. GetFeatures() ends with the light
    // features, so "the last feature" is never the new one.
    private static HashSet<string> _snapshot = new();

    private static HashSet<string> FeatureNames(ModelDoc2 doc)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (doc.FeatureManager.GetFeatures(false) is object[] fs)
            foreach (var o in fs) { try { set.Add(((Feature)o).Name); } catch { } }
        return set;
    }

    private static Feature? NewFeatureSince(ModelDoc2 doc, HashSet<string> before)
    {
        if (doc.FeatureManager.GetFeatures(false) is not object[] fs) return null;
        foreach (var o in fs)
        {
            var f = (Feature)o;
            if (!before.Contains(f.Name)) return f;
        }
        return null;
    }

    private static void Step(string label, Func<Feature?> action)
    {
        Say($"--- build: {label}");
        _snapshot = FeatureNames(doc: CurrentDoc!);
        try
        {
            var f = action();
            Say(f == null
                ? $"    FAILED: no feature returned"
                : $"    created '{f.Name}'  GetTypeName2()='{f.GetTypeName2()}'");
        }
        catch (Exception ex)
        {
            Say($"    FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- report

    private static void Report(ModelDoc2 doc, string seedPath)
    {
        var researched = StaticVerifier.LoadSeed(seedPath);

        Say("");
        Say("=== PHASE 2: live feature-tree readback ===");
        if (doc.FeatureManager.GetFeatures(false) is not object[] features) { Say("no features"); return; }

        foreach (var o in features)
        {
            var f = (Feature)o;
            string typeName;
            try { typeName = f.GetTypeName2(); } catch { continue; }
            if (typeName is "OriginProfileFeature" or "ProfileFeature" or "HistoryFolder" or
                "SensorFolder" or "DetailCabinet" or "CommentFolder" or "SolidBodyFolder" or
                "SurfaceBodyFolder" or "EnvFolder" or "MaterialFolder" or "CoordSys" or
                "LightsFolder" or "FavoriteFolder" or "MateGroup" or "SketchBlockFolder") continue;

            object? def = null;
            try { def = f.GetDefinition(); } catch { }

            Say("");
            Say($"### feature '{f.Name}'  GetTypeName2()='{typeName}'  definition={(def == null ? "NULL" : "ok")}");
            if (def == null) continue;

            var ifaces = ComTypeInspector.FindImplementedInteropInterfaces(def,
                t => t.Name.Contains("FeatureData", StringComparison.OrdinalIgnoreCase));
            Say($"    QI interfaces: {(ifaces.Count == 0 ? "(none)" : string.Join(", ", ifaces.Select(i => i.Name)))}");

            ReadSpecs("researched", def, researched.TryGetValue(typeName, out var rs) ? rs : null);
            ReadSpecs("corrected ", def, Candidates.Corrected.TryGetValue(typeName, out var cs) ? cs : null);

            // Some feature-data objects only surface selection-derived values once
            // AccessSelections has been called; check whether that changes anything.
            if (ComPropertyReader.TryGetMember(def, "AccessSelections", new object?[] { doc, null }, out var acc) &&
                acc is true)
            {
                Say($"    [AccessSelections=true] re-read:");
                ReadSpecs("  corrected+access", def, Candidates.Corrected.TryGetValue(typeName, out var cs2) ? cs2 : null);
                ComPropertyReader.TryGetMember(def, "ReleaseSelectionAccess", null, out _);
            }
        }
    }

    private static void ReadSpecs(string label, object def, IReadOnlyList<SeedSpec>? specs)
    {
        if (specs == null) { Say($"    {label}: (no specs for this type)"); return; }
        foreach (var s in specs)
        {
            var args = s.Args is { Length: > 0 } ? s.Args : null;
            var ok = ComPropertyReader.TryGetMember(def, s.Member, args, out var v);
            Say($"    {label}: {s.Describe(),-34} {(ok ? "READ  " : "FAIL  ")} {Fmt(v)}");
        }
    }

    private static string Fmt(object? v) => v switch
    {
        null => "(null)",
        double d => $"{d:G10}  [mm={d * 1000:G6}] [deg={d * 180 / Math.PI:G6}]",
        _ => $"{v} ({v.GetType().Name})",
    };

    // ---------------------------------------------------------------- helpers

    private static SelectData? MakeSelData(ModelDoc2 doc, int mark)
    {
        var sd = ((SelectionMgr)doc.SelectionManager).CreateSelectData();
        sd.Mark = mark;
        return sd;
    }

    private static bool SelectEntity(ModelDoc2 doc, object entity, int mark, bool append = false)
    {
        var e = (Entity)entity;
        return e.Select4(append, MakeSelData(doc, mark));
    }

    private static bool SelectByName(ModelDoc2 doc, string name, string type, int mark) =>
        doc.Extension.SelectByID2(name, type, 0, 0, 0, true, mark, null, 0);

    private static void SelectPlane(ModelDoc2 doc, string name)
    {
        doc.ClearSelection2(true);
        if (!doc.Extension.SelectByID2(name, "PLANE", 0, 0, 0, false, 0, null, 0))
        {
            // Non-English or trimmed template naming.
            var shortName = name.Replace(" Plane", "");
            doc.Extension.SelectByID2(shortName, "PLANE", 0, 0, 0, false, 0, null, 0);
        }
    }

    private static ModelDoc2? CurrentDoc;

    // Non-folder, non-light feature closest to the end of the tree — the sketch
    // or feature just created. GetFeatures() always tails off with lights.
    private static readonly HashSet<string> TailTypes = new(StringComparer.Ordinal)
    {
        "AmbientLight", "DirectionLight", "PointLight", "SpotLight",
        "NotesAreaFtrFolder", "CommentsFolder", "SelectionSetFolder",
        "DocsFolder", "InkMarkupFolder", "EqnFolder", "SolidBodyFolder",
        "SurfaceBodyFolder", "MaterialFolder", "OriginProfileFeature",
    };

    private static Feature? LastRealFeature(ModelDoc2 doc)
    {
        if (doc.FeatureManager.GetFeatures(false) is not object[] fs || fs.Length == 0) return null;
        for (var i = fs.Length - 1; i >= 0; i--)
        {
            var f = (Feature)fs[i];
            try { if (!TailTypes.Contains(f.GetTypeName2())) return f; } catch { }
        }
        return null;
    }

    // The zoo becomes multibody (the revolve and the Merge=false control boss
    // are separate solids), and GetBodies2 ordering is NOT stable across
    // rebuilds — picking bodies[0] silently starts returning the far body.
    // Everything the harness selects lives near the origin, so pick the body
    // whose box straddles it.
    private static Body2? MainBody(ModelDoc2 doc)
    {
        if (doc is not PartDoc part) return null;
        if (part.GetBodies2((int)swBodyType_e.swSolidBody, true) is not object[] bodies || bodies.Length == 0) return null;
        foreach (var b in bodies)
        {
            var body = (Body2)b;
            // The block spans x,y in [-0.05, 0.05] and z in [0, 0.05]. The
            // revolve body also straddles the origin in x/y (its sketch is on
            // the Right Plane), so the z extent is what actually discriminates.
            if (body.GetBodyBox() is double[] bb &&
                bb[0] < 0.001 && bb[3] > -0.001 &&
                bb[1] < 0.001 && bb[4] > -0.001 &&
                bb[2] < 0.001 && bb[5] > 0.04) return body;
        }
        return (Body2)bodies[0];
    }

    private static IEnumerable<Face2> OriginFaces(ModelDoc2 doc)
    {
        var body = MainBody(doc);
        if (body?.GetFaces() is not object[] faces) yield break;
        foreach (var fo in faces) yield return (Face2)fo;
    }

    private static double[]? BodyBox(ModelDoc2 doc) => MainBody(doc)?.GetBodyBox() as double[];

    private static object? PickVerticalBlockEdge(ModelDoc2 doc, double x, double y)
    {
        var body = MainBody(doc);
        if (body?.GetEdges() is not object[] edges) return null;
        object? best = null;
        var bestDist = double.MaxValue;
        foreach (var eo in edges)
        {
            var e = (Edge)eo;
            if (e.GetStartVertex() is not Vertex v1 || e.GetEndVertex() is not Vertex v2) continue;
            if (v1.GetPoint() is not double[] p1 || v2.GetPoint() is not double[] p2) continue;
            // vertical: same x/y, different z
            if (Math.Abs(p1[0] - p2[0]) > 1e-9 || Math.Abs(p1[1] - p2[1]) > 1e-9) continue;
            if (Math.Abs(p1[2] - p2[2]) < 1e-6) continue;
            var d = Math.Abs(p1[0] - x) + Math.Abs(p1[1] - y);
            if (d < bestDist) { bestDist = d; best = eo; }
        }
        return bestDist < 1e-6 ? best : null;
    }

    private static object? PickHorizontalBlockEdge(ModelDoc2 doc)
    {
        var body = MainBody(doc);
        if (body?.GetEdges() is not object[] edges) return null;
        foreach (var eo in edges)
        {
            var e = (Edge)eo;
            if (e.GetStartVertex() is not Vertex v1 || e.GetEndVertex() is not Vertex v2) continue;
            if (v1.GetPoint() is not double[] p1 || v2.GetPoint() is not double[] p2) continue;
            // along model X; fillets/chamfers shorten the block's corner edges,
            // so accept anything longer than half the block.
            if (Math.Abs(p1[1] - p2[1]) > 1e-9 || Math.Abs(p1[2] - p2[2]) > 1e-9) continue;
            if (Math.Abs(p1[0] - p2[0]) < BlockSize / 2) continue;
            if (Math.Abs(p1[1]) > BlockSize) continue; // ignore the far revolve body
            return eo;
        }
        return null;
    }

    private static object? PickCylindricalFace(ModelDoc2 doc)
    {
        var body = MainBody(doc);
        if (body?.GetFaces() is not object[] faces) return null;
        foreach (var fo in faces)
        {
            var f = (Face2)fo;
            if (f.GetSurface() is Surface s && s.IsCylinder()) return fo;
        }
        return null;
    }

    private static object? PickPlanarFaceByNormal(ModelDoc2 doc, double nx, double ny, double nz)
    {
        object? best = null;
        double bestArea = -1;
        foreach (var f in OriginFaces(doc))
        {
            if (f.GetSurface() is not Surface s || !s.IsPlane()) continue;
            if (f.Normal is not double[] n) continue;
            if (Math.Abs(n[0] - nx) > 1e-6 || Math.Abs(n[1] - ny) > 1e-6 || Math.Abs(n[2] - nz) > 1e-6) continue;
            if (f.GetBox() is double[] b && (Math.Abs(b[0]) > 0.06 || Math.Abs(b[3]) > 0.06)) continue;
            var a = f.GetArea();
            if (a > bestArea) { bestArea = a; best = f; }
        }
        return best;
    }

    private static IEnumerable<Face2> AllFaces(ModelDoc2 doc)
    {
        if (doc is not PartDoc part) yield break;
        if (part.GetBodies2((int)swBodyType_e.swSolidBody, true) is not object[] bodies) yield break;
        foreach (var b in bodies)
            if (((Body2)b).GetFaces() is object[] fs)
                foreach (var f in fs) yield return (Face2)f;
    }

    /// <summary>Largest planar face with the given normal whose box lies in [xMin, xMax], across all bodies.</summary>
    private static object? PickFaceInRegion(ModelDoc2 doc, double nx, double ny, double nz, double xMin, double xMax)
    {
        object? best = null;
        double bestArea = -1;
        foreach (var f in AllFaces(doc))
        {
            if (f.GetSurface() is not Surface s || !s.IsPlane()) continue;
            if (f.Normal is not double[] n) continue;
            if (Math.Abs(n[0] - nx) > 1e-6 || Math.Abs(n[1] - ny) > 1e-6 || Math.Abs(n[2] - nz) > 1e-6) continue;
            if (f.GetBox() is not double[] b) continue;
            if (b[0] < xMin || b[3] > xMax) continue;
            var a = f.GetArea();
            if (a > bestArea) { bestArea = a; best = f; }
        }
        return best;
    }

    private static object? PickTopPlanarFace(ModelDoc2 doc)
    {
        object? best = null;
        double bestZ = double.MinValue;
        foreach (var f in OriginFaces(doc))
        {
            if (f.GetSurface() is not Surface s || !s.IsPlane()) continue;
            if (f.GetBox() is not double[] b) continue;
            if (Math.Abs(b[0]) > 0.06 || Math.Abs(b[3]) > 0.06) continue;
            var z = (b[2] + b[5]) / 2;
            if (z > bestZ) { bestZ = z; best = f; }
        }
        return best;
    }

    // ---------------------------------------------------------------- inspect

    /// <summary>Opens a document so a client (e.g. the MCP smoke test) can target it.</summary>
    public static int OpenDoc(string path)
    {
        var docs = new DocumentManager(new SwConnection());
        var d = docs.OpenDocument(path);
        Console.WriteLine($"opened '{d.Info.Title}' ({d.Info.Path})");
        return 0;
    }

    /// <summary>Closes a document by title/name, leaving everything else open.</summary>
    public static int CloseDoc(string name)
    {
        var connection = new SwConnection();
        var docs = new DocumentManager(connection);
        var d = docs.Resolve(name);
        if (d == null) { Console.WriteLine($"'{name}' not open"); return 1; }
        var title = d.Info.Title;
        connection.GetApp().CloseDoc(title);
        Console.WriteLine($"closed '{title}'");
        Console.WriteLine("still open: " + string.Join(", ", docs.ListOpenDocuments().Select(x => x.Title)));
        return 0;
    }

    /// <summary>Reads an already-open document without modifying it.</summary>
    public static int InspectOpenDoc(string docName, string seedPath)
    {
        var connection = new SwConnection();
        var docs = new DocumentManager(connection);
        var d = docs.Resolve(docName);
        if (d == null) { Console.WriteLine($"document '{docName}' not open"); return 1; }
        Report(d.Model, seedPath);
        return 0;
    }
}

