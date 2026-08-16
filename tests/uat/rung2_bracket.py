"""RUNG 2 - simple bracket: 80 x 40 x 6 mm plate, two through-holes, one filleted corner.

The seed has no cut and no fillet: both come from the enrichment loop
(describe_com_members -> SW API docs -> register_operation).

Plate: corner rectangle (-40,-20) to (40,20) mm on Front Plane, extruded 6 mm.
Holes: 2 x dia 5 mm at (+/-25, 0), through all.
Fillet: R6 on one vertical corner edge.
"""
import json
import sys
from uat_client import Session, bbox_mm

CUT = {
    "name": "cut_extrude",
    "summary": "Cut-extrudes the pre-selected sketch profile, removing material. Select the sketch (select_by_id type SKETCH mark 0), exit sketch mode, then call. endCondition1=0 blind (uses depth1), 1 through-all. Mirrors extrude_boss but calls FeatureCut4.",
    "scope": "document",
    "target": "FeatureManager",
    "kind": "method",
    "member": "FeatureCut4",
    "requires": [
        {"check": "documentType", "value": "Part"},
        {"check": "notInSketchMode"},
        {"check": "selectionCount", "min": 1},
    ],
    "params": [
        {"name": "singleDirection", "type": "bool", "default": True, "description": "Sd"},
        {"name": "flipSideToCut", "type": "bool", "default": False, "description": "Flip - cut the other side of the profile"},
        {"name": "reverseDirection", "type": "bool", "default": False, "description": "Dir"},
        {"name": "endCondition1", "type": "enum", "enum": "swEndConditions_e", "default": 0, "description": "T1: 0 Blind, 1 ThroughAll"},
        {"name": "endCondition2", "type": "enum", "enum": "swEndConditions_e", "default": 0},
        {"name": "depth1", "type": "length", "default": 0, "description": "D1 - blind depth dir 1"},
        {"name": "depth2", "type": "length", "default": 0},
        {"name": "draftOn1", "type": "bool", "default": False},
        {"name": "draftOn2", "type": "bool", "default": False},
        {"name": "draftOutward1", "type": "bool", "default": False},
        {"name": "draftOutward2", "type": "bool", "default": False},
        {"name": "draftAngle1", "type": "angle", "default": 0},
        {"name": "draftAngle2", "type": "angle", "default": 0},
        {"name": "offsetReverse1", "type": "bool", "default": False},
        {"name": "offsetReverse2", "type": "bool", "default": False},
        {"name": "translateSurface1", "type": "bool", "default": False},
        {"name": "translateSurface2", "type": "bool", "default": False},
        {"name": "normalCut", "type": "bool", "default": False},
        {"name": "useFeatureScope", "type": "bool", "default": True},
        {"name": "useAutoSelect", "type": "bool", "default": True},
        {"name": "assemblyFeatureScope", "type": "bool", "default": False},
        {"name": "autoSelectComponents", "type": "bool", "default": False},
        {"name": "propagateFeatureToParts", "type": "bool", "default": False},
        {"name": "startCondition", "type": "enum", "enum": "swStartConditions_e", "default": 0},
        {"name": "startOffset", "type": "length", "default": 0},
        {"name": "flipStartOffset", "type": "bool", "default": False},
        {"name": "optimizeGeometry", "type": "bool", "default": False},
    ],
    "returns": {"type": "feature"},
    "verify": [{"check": "returnNotNull"}, {"check": "featureCountIncreased", "by": 1}],
}

FILLET = {
    "name": "fillet_constant_radius",
    "summary": "Constant-radius fillet on the pre-selected edge(s) or face(s) (FeatureFillet3). Select the edge first with select_by_id type EDGE, mark 0 (name '' + x/y/z coordinates of a point ON the edge works). options=195 (Propagate|UniformRadius|AttachEdges|KeepFeatures) is the combination that works; plain propagate returns null.",
    "scope": "document",
    "target": "FeatureManager",
    "kind": "method",
    "member": "FeatureFillet3",
    "requires": [
        {"check": "documentType", "value": "Part"},
        {"check": "notInSketchMode"},
        {"check": "selectionCount", "min": 1},
    ],
    "params": [
        {"name": "options", "type": "int", "default": 195, "enum": "swFeatureFilletOptions_e", "description": "195 = Propagate|UniformRadius|AttachEdges|KeepFeatures"},
        {"name": "radius", "type": "length", "required": True, "description": "R1 - constant fillet radius"},
        {"name": "rho", "type": "double", "default": 0},
        {"name": "setbackDistance", "type": "length", "default": 0},
        {"name": "filletType", "type": "enum", "enum": "swFeatureFilletType_e", "default": 0, "description": "0 = simple/constant radius"},
        {"name": "overflowType", "type": "int", "default": 0},
        {"name": "conicTypeForCurvature", "type": "int", "default": 0},
        {"name": "radiiArray", "type": "comNull"},
        {"name": "rhoArray", "type": "comNull"},
        {"name": "setbackDistances", "type": "comNull"},
        {"name": "pointRadiusArray", "type": "comNull"},
        {"name": "setbackVertexArray", "type": "comNull"},
        {"name": "conicRhoArray", "type": "comNull"},
        {"name": "conicRhoOrCurvatureArray", "type": "comNull"},
    ],
    "returns": {"type": "feature"},
    "verify": [{"check": "returnNotNull"}, {"check": "featureCountIncreased", "by": 1}],
}


def rect(s, doc, x1, y1, x2, y2):
    """Corner rectangle out of 4 seed create_line calls (mm)."""
    pts = [(x1, y1), (x2, y1), (x2, y2), (x1, y2), (x1, y1)]
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        s.op("create_line", {"x1": f"{ax} mm", "y1": f"{ay} mm",
                             "x2": f"{bx} mm", "y2": f"{by} mm"}, doc, echo=False)


with Session(quiet=True) as s:
    print("--- register enrichment recipes ---")
    for rec in (CUT, FILLET):
        print(json.dumps(s.call("register_operation", {"recipe": rec}, echo=False), indent=2))

    r = s.op("new_part")
    doc = r["return"]["title"]
    print(f"### scratch doc: {doc}")
    print("open docs now:", [d["title"] for d in s.call("list_open_documents", {}, echo=False)["documents"]])

    # --- base plate ---
    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    rect(s, doc, -40, -20, 40, 20)
    s.op("exit_sketch", {}, doc, echo=False)
    s.op("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc, echo=False)
    s.op("extrude_boss", {"depth1": "6 mm"}, doc)
    info = s.call("get_part_info", {"documentName": doc}, echo=False)
    print("plate bbox mm:", bbox_mm(info), "min z:", info["boundingBox"]["min"]["z"])
    zmin = info["boundingBox"]["min"]["z"]
    zmax = info["boundingBox"]["max"]["z"]

    # --- two through holes ---
    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    s.op("create_circle_by_radius", {"centerX": "25 mm", "centerY": 0, "radius": "2.5 mm"}, doc, echo=False)
    s.op("create_circle_by_radius", {"centerX": "-25 mm", "centerY": 0, "radius": "2.5 mm"}, doc, echo=False)
    s.op("exit_sketch", {}, doc, echo=False)
    s.op("select_by_id", {"name": "Sketch2", "type": "SKETCH", "mark": 0}, doc, echo=False)
    res = s.op("cut_extrude", {"endCondition1": 1}, doc, must_succeed=False)
    print("cut attempt dir=false:", res.get("success"), res.get("error"))
    if not res.get("success"):
        s.op("select_by_id", {"name": "Sketch2", "type": "SKETCH", "mark": 0}, doc, echo=False)
        res = s.op("cut_extrude", {"endCondition1": 1, "reverseDirection": True}, doc, must_succeed=False)
        print("cut attempt dir=true:", res.get("success"), res.get("error"))

    info = s.call("get_part_info", {"documentName": doc}, echo=False)
    print("after cut, features:", [(f["name"], f["typeName"]) for f in info["features"]
                                   if f["typeName"] in ("Extrusion", "ICE", "Cut")])
    print("mass after cut:", info["mass"])

    # --- fillet a vertical corner edge ---
    zmid = (zmin + zmax) / 2
    s.op("clear_selection", {}, doc, echo=False)
    sel = s.op("select_by_id", {"name": "", "type": "EDGE", "x": 0.040, "y": 0.020, "z": zmid,
                                "mark": 0}, doc, must_succeed=False)
    print("edge select:", sel.get("success"), sel.get("error"), sel.get("documentState"))
    if sel.get("success"):
        fr = s.op("fillet_constant_radius", {"radius": "6 mm"}, doc, must_succeed=False)
        print("fillet:", json.dumps(fr, indent=2))

    s.op("rebuild", {}, doc, echo=False)
    info = s.call("get_part_info", {"documentName": doc}, echo=False)
    print("FINAL bbox mm:", bbox_mm(info))
    print("FINAL mass kg:", info["mass"])
    print("FINAL features:", [(f["name"], f["typeName"], f.get("data")) for f in info["features"]
                              if f["typeName"] in ("Extrusion", "ICE", "Cut", "Fillet")])
    print(f"### DOC TITLE FOR CLEANUP: {doc}")
