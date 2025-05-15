import win32com.client
import pythoncom


def get_solidworks_part_info(sw_app):
    """
    Retrieves comprehensive information about the currently opened SolidWorks part document.

    Args:
        sw_app: The SolidWorks application object obtained from connect_to_solidworks().

    Returns:
        A dictionary containing comprehensive information about the part,
        or None if an error occurs or no part document is active.
    """
    if sw_app is None:
        print("SolidWorks application object is None. Cannot proceed.")
        return None

    try:
        # Ensure constants are loaded
        # This makes constants like sw_app.constants.swDocPART available
        if not hasattr(sw_app, "constants"):
            # This can happen if makepy hasn't been run or in some environments.
            # As a fallback, we might need to use raw integer values if this fails,
            # or ensure win32com.client.gencache.EnsureModule('{GUID_OF_SldWorks_TYPELIB}') is run.
            # For now, we assume constants will be available via sw_app.constants
            # For SldWorks 2015 Type Library, GUID is {83A33D31-27C5-4CE8-80FA-BFF57866257F}
            # Example: win32com.client.gencache.EnsureModule('{83A33D31-27C5-4CE8-80FA-BFF57866257F}', 0, 1, 0)
            # The version numbers (1,0) might change.
            # A more robust way is to let Dispatch load them.
            pass

        sw_model = sw_app.ActiveDoc
        if sw_model is None:
            print("No active document in SolidWorks.")
            return None

        doc_type = sw_model.GetType
        # swDocPART = 1 (typically, use constants for robustness)
        swDocPART_const = sw_app.constants.swDocPART
        if doc_type != swDocPART_const:
            doc_type_map = {
                sw_app.constants.swDocASSEMBLY: "Assembly",
                sw_app.constants.swDocDRAWING: "Drawing",
            }
            print(
                f"Active document is not a Part. It is a {doc_type_map.get(doc_type, 'Unknown Type')}."
            )
            return None

        print(f"Processing Part Document: {sw_model.GetPathName}")
        part_info = {}

        # Get IPartDoc for part-specific methods
        sw_part = win32com.client.CastTo(sw_model, "IPartDoc")
        sw_model_doc_ext = sw_model.Extension
        sw_sel_mgr = sw_model.SelectionManager

        # --- 1. Document-Level Information ---
        part_info["file_path"] = sw_model.GetPathName()
        part_info["document_title"] = sw_model.GetTitle()

        summary_info = {}
        try:
            # Common summary info fields (refer to swSumInfoField_e enumeration)
            # These constants should be available via sw_app.constants
            summary_fields = {
                "Title": sw_app.constants.swSumInfoTitle,
                "Author": sw_app.constants.swSumInfoAuthor,
                "Keywords": sw_app.constants.swSumInfoKeywords,
                "Subject": sw_app.constants.swSumInfoSubject,
                "Comment": sw_app.constants.swSumInfoComment,
                "CreateDate": sw_app.constants.swSumInfoCreateDate,  # Might be localized string
                "LastSavedDate": sw_app.constants.swSumInfoLastSavedDate,  # Might be localized string
                "LastSavedBy": sw_app.constants.swSumInfoLastSavedBy,
            }
            for name, field_id in summary_fields.items():
                summary_info[name] = sw_model.SummaryInfo(field_id)
        except Exception as e:
            summary_info["error"] = f"Could not retrieve some summary info: {str(e)}"
        part_info["summary_info"] = summary_info

        file_custom_props = {}
        try:
            custom_prop_mgr = sw_model_doc_ext.CustomPropertyManager(
                ""
            )  # Empty string for file-specific
            prop_names = custom_prop_mgr.GetNames()
            if prop_names:
                for prop_name in prop_names:
                    val, resolved_val, was_resolved, link_to_prop = (
                        custom_prop_mgr.Get5(prop_name)
                    )
                    file_custom_props[prop_name] = resolved_val
        except Exception as e:
            file_custom_props["error"] = (
                f"Could not retrieve file custom properties: {str(e)}"
            )
        part_info["custom_properties_file"] = file_custom_props

        try:
            # Get model's linear unit for reference
            # swUnitsLinear_e defines enum for METER, MILLIMETER, etc.
            # swUserPreferenceIntegerValue_e.swUnitsLinear
            unit_system_enum = sw_model_doc_ext.GetUserPreferenceIntegerValue(
                sw_app.constants.swUnitsLinear
            )
            # We can map this enum to a string if needed, or get the unit name string directly
            unit_name_string = sw_model_doc_ext.GetUserPreferenceStringValue(
                sw_app.constants.swUnitsLinearName
            )
            part_info["model_units_linear"] = (
                f"{unit_name_string} (Enum: {unit_system_enum})"
            )
        except Exception as e:
            part_info["model_units_linear"] = f"Error getting units: {str(e)}"

        # --- 2. Configuration Information ---
        try:
            active_conf = sw_model.GetActiveConfiguration()
            part_info["active_configuration_name"] = active_conf.Name

            configurations_data = []
            conf_names = sw_model.GetConfigurationNames()
            if conf_names:
                for conf_name in conf_names:
                    conf_data = {"name": conf_name, "custom_properties": {}}
                    current_conf = sw_model.GetConfigurationByName(conf_name)
                    if current_conf:
                        conf_prop_mgr = current_conf.CustomPropertyManager
                        conf_prop_names = conf_prop_mgr.GetNames()
                        if conf_prop_names:
                            for prop_name in conf_prop_names:
                                val, resolved_val, was_resolved, link_to_prop = (
                                    conf_prop_mgr.Get5(prop_name)
                                )
                                conf_data["custom_properties"][prop_name] = resolved_val
                    configurations_data.append(conf_data)
            part_info["configurations"] = configurations_data
        except Exception as e:
            part_info["configurations_error"] = (
                f"Could not retrieve configuration data: {str(e)}"
            )

        # --- 3. Standard Geometry (Planes, Origin) ---
        standard_geometry = {}
        try:
            geo_to_find = {
                "Front Plane": "PLANE",
                "Top Plane": "PLANE",
                "Right Plane": "PLANE",
                "Origin": "ORIGINFEATURE",  # As per document
            }
            for name, type_str in geo_to_find.items():
                # swSelectOptionDefault = 0
                # For SelectByID2, the Mark parameter is not directly used for selection criteria,
                # but for subsequent operations on selected objects.
                # For selecting, it's often 0 or a specific value if needed.
                # The Callout parameter is an IDispatch, can be None.
                # The SelectOption parameter is from swSelectOption_e.
                # swSelectOptionDefault = 0
                select_option = sw_app.constants.swSelectOptionDefault

                # Clear previous selections before selecting a new entity by ID
                sw_model.ClearSelection2(True)

                selected = sw_model_doc_ext.SelectByID2(
                    name, type_str, 0, 0, 0, False, 0, None, select_option
                )
                if selected:
                    # Mark for GetSelectedObject6: -1 means any mark
                    selected_obj = sw_sel_mgr.GetSelectedObject6(1, -1)
                    if selected_obj:
                        # This selected_obj is typically an IFeature
                        feat = win32com.client.CastTo(selected_obj, "IFeature")
                        standard_geometry[name.lower().replace(" ", "_")] = {
                            "name": feat.Name,
                            "type": feat.GetTypeName2(),
                        }
                    else:
                        standard_geometry[name.lower().replace(" ", "_")] = {
                            "error": "Found by SelectByID2 but GetSelectedObject6 failed."
                        }
                else:
                    standard_geometry[name.lower().replace(" ", "_")] = {
                        "error": f"Not found by SelectByID2 using name '{name}' and type '{type_str}'."
                    }
            sw_model.ClearSelection2(True)  # Clear selection after we are done
        except Exception as e:
            standard_geometry["error"] = (
                f"Could not retrieve standard geometry: {str(e)}"
            )
        part_info["standard_geometry"] = standard_geometry

        # --- 4. Feature Tree Information ---
        features_list = []
        try:
            feature = sw_model.IFirstFeature()
            while feature:
                features_list.append(_get_feature_data(feature, sw_app, sw_model))
                feature = feature.IGetNextFeature()  # Use I-prefixed version as per doc
        except Exception as e:
            part_info["features_error"] = f"Error traversing features: {str(e)}"
        part_info["features"] = features_list

        # --- 5. Solid Body Geometry (B-Rep Topology) ---
        bodies_data = []
        try:
            # swSolidBody = 0, swSheetBody = 1 (typically)
            body_types_to_get = {
                "solid": sw_app.constants.swSolidBody,
                "sheet": sw_app.constants.swSheetBody,
            }
            for body_type_name, body_type_enum in body_types_to_get.items():
                # GetBodies2 returns a Variant array of IBody2 objects
                bodies_variant = sw_part.GetBodies2(body_type_enum)
                if bodies_variant:
                    for body_disp in bodies_variant:
                        body = win32com.client.CastTo(body_disp, "IBody2")
                        bodies_data.append(
                            _get_body_details(body, sw_app, body_type_name)
                        )
        except Exception as e:
            part_info["bodies_error"] = f"Error retrieving body data: {str(e)}"
        part_info["bodies"] = bodies_data

        # --- 6. Mass Properties ---
        # Only calculate if there are solid bodies, as mass properties are typically for solids.
        has_solid_body = any(b.get("type") == "solid" for b in bodies_data if b)
        if has_solid_body:
            mass_props_data = {}
            try:
                # GetMassProperties2 returns a Variant array of doubles
                # The exact content and order can vary slightly or depend on parameters not used here.
                # Typical order: CoMx, CoMy, CoMz, Volume, Area, Mass, Ix, Iy, Iz, Ixy, Ixz, Iyz (at origin)
                # Or: CoMx,CoMy,CoMz,Volume,Area,Mass, Lxx,Lyy,Lzz,Lxy,Lxz,Lyz (at CoM with principal axes)
                # The document says: "center of mass, and moments of inertia".
                # This usually means values are relative to the part's origin and coordinate axes.
                # It's best to consult the specific API documentation for GetMassProperties2 for the exact definition.
                # For now, we'll assume a common 12-value array.
                # Adding a density is required for mass, if not set, mass will be based on default or material.
                # To ensure mass is calculated, a material should be assigned in SW or density set via API.
                # For this function, we query what SW reports.

                # Check if density is applied
                material_props = sw_model_doc_ext.GetMaterialPropertyValues2(
                    active_conf.Name, None
                )  # (config_name, database_path)
                if (
                    material_props
                ):  # Density, YieldStrength, TensileStrength, ElasticModulus, PoissonRatio, ShearModulus, ThermalExpansionCoeff, SpecificHeat, ThermalConductivity, HardeningFactor, MaterialName, Database, HatchFileName, HatchFileScale, MassDensityState
                    mass_props_data["material_name"] = material_props[10]
                    mass_props_data["material_density_kg_m3"] = material_props[
                        0
                    ]  # Mass density in kg/m^3
                else:
                    mass_props_data["material_info"] = (
                        "No material properties found for active configuration or default density used."
                    )

                props = sw_model_doc_ext.GetMassProperties2(
                    0, None, None
                )  # (AccuracyLevel, DensityOverride, DensityUnitSystem)
                # 0 for swAccuracyNone, None for no override
                if props:
                    mass_props_data["center_of_mass"] = (
                        props[0],
                        props[1],
                        props[2],
                    )  # meters
                    mass_props_data["volume"] = props[3]  # cubic meters
                    mass_props_data["surface_area"] = props[4]  # square meters
                    mass_props_data["mass"] = props[5]  # kilograms
                    # Moments of inertia relative to the output coordinate system (usually part origin)
                    # (Ix, Iy, Iz, Ixy, Ixz, Iyz)
                    mass_props_data["moments_of_inertia_at_origin"] = {
                        "Ix": props[6],
                        "Iy": props[7],
                        "Iz": props[8],
                        "Ixy": props[9],
                        "Ixz": props[10],
                        "Iyz": props[11],
                    }
                    # Units for moments of inertia: kg*m^2
                else:
                    mass_props_data["error"] = "GetMassProperties2 returned no data."
            except Exception as e:
                mass_props_data["error"] = (
                    f"Could not retrieve mass properties: {str(e)}"
                )
            part_info["mass_properties"] = mass_props_data
        else:
            part_info["mass_properties"] = (
                "No solid bodies found to calculate mass properties."
            )

        return part_info

    except pythoncom.com_error as ce:
        print(f"A COM Error occurred: {ce}")
        return {"error": f"COM Error: {ce}"}
    except AttributeError as ae:
        if "constants" in str(ae):
            print(
                f"AttributeError: {ae}. This might be due to SolidWorks constants not being loaded."
            )
            print(
                "Try running 'python -m win32com.client.makepy -i \"SOLIDWORKS SldWorks Type Library\"' or ensure SolidWorks is properly installed and registered for COM."
            )
            return {"error": f"AttributeError (likely constants issue): {ae}"}
        print(f"An AttributeError occurred: {ae}")
        return {"error": f"AttributeError: {ae}"}
    except Exception as e:
        print(f"An unexpected error occurred in get_solidworks_part_info: {e}")
        import traceback

        traceback.print_exc()
        return {"error": f"Unexpected error: {str(e)}"}
    finally:
        # Optional: Clear selections or reset any states if necessary
        if "sw_model" in locals() and sw_model is not None:
            sw_model.ClearSelection2(True)
        # COM CoUninitialize is typically handled by pythoncom when the script ends
        # or when the last COM object is released if CoInitialize was called.
        # For explicit control: pythoncom.CoUninitialize() if you called CoInitialize()


# --- Helper Functions for Feature and Geometry Details ---


def _get_feature_data(feature, sw_app, sw_model):
    """Helper to extract data for a single feature."""
    feat_data = {
        "name": feature.Name,
        "type_name": feature.GetTypeName2(),
        "is_suppressed": feature.IsSuppressed(),
        "error_code": feature.GetErrorCode2(),  # See swFeatureError_e
        "definition_details": {},
        "specific_details": {},
        "sub_features": [],
    }

    # Get definition details (parameters used to define the feature)
    try:
        definition = feature.GetDefinition()  # Returns IDispatch
        # Casting to specific definition types (e.g., IExtrudeFeatureData2)
        # can be complex and depends on the feature type.
        # For this example, we'll provide a placeholder.
        # feat_data["definition_details"] = _get_feature_definition_details(feature, definition, sw_app)
        if definition:  # Check if definition object is not None
            feat_data["definition_details"][
                "info"
            ] = f"Raw definition object available for {feat_data['type_name']}. Further parsing needed."
            # Example for Extrude (requires IExtrudeFeatureData2)
            if feat_data["type_name"] in [
                "Extrusion",
                "Boss",
                "Cut",
                "BossThin",
                "ExtrusionThin",
            ]:
                try:
                    extrude_def = win32com.client.CastTo(
                        definition, "IExtrudeFeatureData2"
                    )
                    feat_data["definition_details"]["end_condition_type_dir1"] = (
                        extrude_def.Type[0]
                    )  # swEndConditionType_e for dir1
                    feat_data["definition_details"]["depth_dir1"] = (
                        extrude_def.GetDepth(True)
                    )
                    feat_data["definition_details"]["reverse_dir1"] = (
                        extrude_def.GetReverseDirection(True)
                    )
                    if (
                        extrude_def.Type[1]
                        != sw_app.constants.swEndConditionType_e_swEndCondUnknown
                    ):  # Check if second direction is used
                        feat_data["definition_details"]["end_condition_type_dir2"] = (
                            extrude_def.Type[1]
                        )
                        feat_data["definition_details"]["depth_dir2"] = (
                            extrude_def.GetDepth(False)
                        )
                        feat_data["definition_details"]["reverse_dir2"] = (
                            extrude_def.GetReverseDirection(False)
                        )
                except Exception as e_extrude:
                    feat_data["definition_details"][
                        "extrude_error"
                    ] = f"Could not get extrude definition details: {str(e_extrude)}"

    except Exception as e:
        feat_data["definition_details"]["error"] = f"Could not get definition: {str(e)}"

    # Get specific feature details (runtime state/result of the feature)
    try:
        specific_feature_obj = feature.GetSpecificFeature2()  # Returns IDispatch
        if specific_feature_obj:
            # feat_data["specific_details"] = _get_feature_specific_details(feature, specific_feature_obj, sw_app, sw_model)
            feat_data["specific_details"][
                "info"
            ] = f"Raw specific feature object available for {feat_data['type_name']}. Further parsing needed."
            if feat_data["type_name"] == "Sketch":
                try:
                    sketch = win32com.client.CastTo(specific_feature_obj, "ISketch")
                    feat_data["specific_details"]["sketch_info"] = _get_sketch_details(
                        sketch, sw_model, sw_app
                    )
                except Exception as e_sketch:
                    feat_data["specific_details"][
                        "sketch_error"
                    ] = f"Could not cast or process sketch: {str(e_sketch)}"
            elif feat_data["type_name"] == "RefPlane":
                try:
                    ref_plane = win32com.client.CastTo(
                        specific_feature_obj, "IRefPlane"
                    )
                    # transform = ref_plane.Transform # MathTransform
                    # root_point = transform.ArrayData[12:15] # X, Y, Z of origin
                    # normal_vector = transform.ArrayData[6:9] # Z-axis (normal)
                    # feat_data["specific_details"]["plane_origin"] = root_point
                    # feat_data["specific_details"]["plane_normal"] = normal_vector
                    feat_data["specific_details"][
                        "info"
                    ] = "Reference Plane details (transform) available."
                except Exception as e_refplane:
                    feat_data["specific_details"][
                        "refplane_error"
                    ] = f"Could not get RefPlane details: {str(e_refplane)}"
    except Exception as e:
        feat_data["specific_details"][
            "error"
        ] = f"Could not get specific feature: {str(e)}"

    # Traverse sub-features
    try:
        sub_feature = feature.IGetFirstSubFeature()
        while sub_feature:
            feat_data["sub_features"].append(
                _get_feature_data(sub_feature, sw_app, sw_model)
            )
            sub_feature = sub_feature.IGetNextSubFeature()
    except Exception as e:
        feat_data["sub_features_error"] = f"Error traversing sub-features: {str(e)}"

    return feat_data


def _get_sketch_details(sketch, sw_model, sw_app):
    """
    Helper to extract details from an ISketch object.
    Note: Accessing detailed sketch geometry might require the sketch to be in edit mode.
    This function attempts to read data without forcing edit mode, which may be incomplete.
    """
    sketch_info = {"segments": [], "points": []}

    # Check if sketch is active for editing (optional, for context)
    # active_sketch = sw_model.IGetActiveSketch2()
    # sketch_info["is_active_for_edit"] = (active_sketch is not None and active_sketch == sketch)

    try:
        # Get Sketch Segments
        segments_variant = sketch.GetSketchSegments()
        if segments_variant:
            for seg_disp in segments_variant:
                segment = win32com.client.CastTo(seg_disp, "ISketchSegment")
                seg_data = {"type_enum": segment.GetType()}  # swSketchSegments_e
                # Map enum to string for readability
                seg_type_map = {
                    sw_app.constants.swSketchLINE: "Line",
                    sw_app.constants.swSketchARC: "Arc",
                    sw_app.constants.swSketchELLIPSE: "Ellipse",
                    sw_app.constants.swSketchSPLINE: "Spline",
                    sw_app.constants.swSketchPARABOLA: "Parabola",
                    # Add other types as needed
                }
                seg_data["type_name"] = seg_type_map.get(
                    seg_data["type_enum"], "UnknownSegment"
                )

                # Get specific segment data (example for line and arc)
                if seg_data["type_enum"] == sw_app.constants.swSketchLINE:
                    sline = win32com.client.CastTo(segment, "ISketchLine")
                    start_pt = sline.GetStartPoint2()  # Returns array (X,Y,Z)
                    end_pt = sline.GetEndPoint2()  # Returns array (X,Y,Z)
                    seg_data["start_point"] = tuple(start_pt) if start_pt else None
                    seg_data["end_point"] = tuple(end_pt) if end_pt else None
                elif seg_data["type_enum"] == sw_app.constants.swSketchARC:
                    sarc = win32com.client.CastTo(segment, "ISketchArc")
                    center_pt = sarc.GetCenterPoint2()
                    start_pt = sarc.GetStartPoint2()
                    end_pt = sarc.GetEndPoint2()
                    seg_data["center_point"] = tuple(center_pt) if center_pt else None
                    seg_data["start_point"] = tuple(start_pt) if start_pt else None
                    seg_data["end_point"] = tuple(end_pt) if end_pt else None
                    seg_data["radius"] = sarc.GetRadius()
                    seg_data["is_circle"] = sarc.IsCircle()

                sketch_info["segments"].append(seg_data)
    except Exception as e:
        sketch_info["segments_error"] = f"Error getting sketch segments: {str(e)}"

    try:
        # Get Sketch Points
        points_variant = sketch.GetSketchPoints()
        if points_variant:
            for pt_disp in points_variant:
                point = win32com.client.CastTo(pt_disp, "ISketchPoint")
                sketch_info["points"].append({"x": point.X, "y": point.Y, "z": point.Z})
    except Exception as e:
        sketch_info["points_error"] = f"Error getting sketch points: {str(e)}"

    sketch_info["note"] = (
        "Sketch details retrieved without forcing edit mode; may be incomplete for some parameters."
    )
    return sketch_info


def _get_body_details(body, sw_app, body_type_name):
    """Helper to extract details from an IBody2 object."""
    body_data = {
        "type": body_type_name,
        "name": body.Name,  # Bodies can be named
        "visible": body.Visible,
        "material_property_values": None,  # Could be extensive, get from ModelDocExtension if needed globally
        "faces": [],
        "edges": [],
        "vertices": [],
    }

    try:
        # Get Material applied to this body (if any)
        # This returns an array: [Density, YieldS, TensileS, ElasticMod, PoissonR, ShearMod, ThermalExpCoeff, SpecificHeat, ThermalConductivity, HardeningFactor, MaterialName, DatabaseName, HatchFileName, HatchScale, MassDensityState]
        # body_data["material_property_values"] = body.MaterialPropertyValues2
        # Note: MaterialPropertyValues2 is on IBody2, but might be configuration specific.
        # The document suggests getting it from IModelDocExtension for the active config.
        pass  # Placeholder for now, as it can be complex with configurations.
    except Exception as e:
        body_data["material_error"] = f"Could not get body material: {str(e)}"

    # Faces
    try:
        faces_variant = body.GetFaces()
        if faces_variant:
            for face_disp in faces_variant:
                face = win32com.client.CastTo(face_disp, "IFace2")
                face_data = {"id": face.GetFaceId()}
                # Get underlying surface type
                surface = face.IGetSurface()  # Returns ISurface
                if surface:
                    face_data["surface_type"] = _get_surface_type_name(surface, sw_app)
                    # Optionally, get surface parameters (can be very detailed)
                    # if face_data["surface_type"] == "Plane":
                    #     plane_params = surface.PlaneParams # Normal (x,y,z), RootPoint (x,y,z)
                    #     face_data["plane_normal"] = plane_params[0:3]
                    #     face_data["plane_root_point"] = plane_params[3:6]
                body_data["faces"].append(face_data)
    except Exception as e:
        body_data["faces_error"] = f"Error getting faces: {str(e)}"
    body_data["face_count"] = len(body_data["faces"])

    # Edges
    try:
        edges_variant = body.GetEdges()
        if edges_variant:
            for edge_disp in edges_variant:
                edge = win32com.client.CastTo(edge_disp, "IEdge")
                edge_data = {}
                curve = edge.IGetCurve()  # Returns ICurve
                if curve:
                    edge_data["curve_type"] = _get_curve_type_name(curve, sw_app)
                    # Optionally, get curve parameters
                    # if edge_data["curve_type"] == "Line":
                    #    line_params = curve.LineParams # StartPoint(x,y,z), EndPoint(x,y,z) or Direction(x,y,z), Origin(x,y,z)
                    #    edge_data["line_params"] = line_params
                body_data["edges"].append(edge_data)
    except Exception as e:
        body_data["edges_error"] = f"Error getting edges: {str(e)}"
    body_data["edge_count"] = len(body_data["edges"])

    # Vertices
    try:
        vertices_variant = body.GetVertices()
        if vertices_variant:
            for vert_disp in vertices_variant:
                vertex = win32com.client.CastTo(vert_disp, "IVertex")
                coords = vertex.GetPoint()  # Returns array (X,Y,Z)
                body_data["vertices"].append(
                    {"coordinates": tuple(coords) if coords else None}
                )
    except Exception as e:
        body_data["vertices_error"] = f"Error getting vertices: {str(e)}"
    body_data["vertex_count"] = len(body_data["vertices"])

    return body_data


def _get_surface_type_name(surface, sw_app):
    """Helper to get a string name for an ISurface type."""
    if surface.IsPlane():
        return "Plane"
    if surface.IsCylinder():
        return "Cylinder"
    if surface.IsCone():
        return "Cone"
    if surface.IsSphere():
        return "Sphere"
    if surface.IsTorus():
        return "Torus"
    if surface.IsBspline():
        return "BsplineSurface"  # BSplineSurface (NurbsSurface)
    # Add more types as per ISurface documentation (e.g., IsRevolved, IsExtruded, IsOffset)
    return "UnknownSurface"


def _get_curve_type_name(curve, sw_app):
    """Helper to get a string name for an ICurve type."""
    if curve.IsLine():
        return "Line"
    if curve.IsCircle():
        return "Circle"
    if curve.IsEllipse():
        return "Ellipse"
    if curve.IsBspline():
        return "BsplineCurve"  # NurbsCurve
    # Add more types as per ICurve documentation (e.g., IsBlend)
    return "UnknownCurve"
