# **A Comprehensive Registry of SolidWorks.NET API Methods for CAD Automation**

## **1\. Introduction**

The SolidWorks Application Programming Interface (API) provides a powerful mechanism for automating and customizing the SolidWorks Computer-Aided Design (CAD) software. This document serves as a technical registry of common SolidWorks functionalities and their corresponding.NET API calls, with a specific focus on facilitating the development of Python wrappers for automation tools. The information compiled herein is derived from analyses of the SolidWorks API documentation structure and specific API method details.

### **1.1. Purpose and Scope of the API Registry**

The primary purpose of this registry is to furnish developers, particularly those working with Python, a detailed and structured list of API methods for executing common SolidWorks operations. These operations encompass the creation of standard parametric features (e.g., extrusions, fillets, revolves) and the utilization of common sketch tooling (e.g., line and circle creation, sketch offsetting). For each API method, this document aims to provide a comprehensive breakdown of its parameters, including their names, data types, and functional descriptions, thereby streamlining the process of creating robust and efficient automation scripts.

The scope is centered on the.NET API, which is well-suited for interfacing with Python. While SolidWorks supports various programming languages for its API, the.NET framework offers a common ground for interoperability. This registry focuses on the IFeatureManager and ISketchManager interfaces, along with related data objects and utility interfaces like IModelDocExtension and ISketchRelationManager, which are fundamental for part and sketch manipulation.

### **1.2. Navigating the SolidWorks API Documentation**

The official SolidWorks API documentation is typically structured as a hierarchical system, often presented with a tree view in its help interface, allowing navigation through various namespaces, interfaces, methods, and enumerations.1 The primary namespace for core SolidWorks functionality is SolidWorks.Interop.sldworks.2 Within this namespace, key interfaces such as IFeatureManager (for feature creation and management) and ISketchManager (for sketch entity creation and manipulation) are found. Understanding this structure is crucial for locating detailed information beyond this registry. Access to specific interface documentation usually involves navigating to the "SOLIDWORKS API Help" section, then to "Interfaces" or an "Object Model" section, and finally selecting the desired interface to view its members (methods and properties).1

### **1.3. SolidWorks API Fundamentals for Automation**

The SolidWorks API is fundamentally a Component Object Model (COM) based interface.3 This architecture allows various programming languages, including Visual Basic for Applications (VBA), VB.NET, C\#, C++, and C++/CLI, to interact with SolidWorks functionalities.1 The.NET compatibility is particularly relevant, as it enables languages like Python to interface with the API, typically through libraries such as pythonnet.

A critical aspect of working with the SolidWorks API is adherence to versioning recommendations. It is generally advised to utilize the most current version of the API available for the target SolidWorks release and to avoid undocumented or obsolete API calls.1 Obsolete methods, while sometimes still functional, may be removed or behave unpredictably in future SolidWorks versions. This registry will, where information is available, note methods that have been superseded, guiding developers towards more contemporary and supported API practices. For instance, the evolution of feature creation often involves newer methods with more structured parameter handling, such as using dedicated FeatureData objects instead of long lists of parameters in a single function call.

## **2\. SolidWorks API Registry for Automation**

This section provides a detailed catalog of SolidWorks API methods pertinent to common CAD operations. The organization prioritizes feature creation followed by sketch-level functionalities.

A prevalent pattern in the modern SolidWorks API for creating complex features involves a two-step process:

1. Obtaining a feature-specific data object by calling IFeatureManager::CreateDefinition(swFeatureNameID\_e As Long), where swFeatureNameID\_e is an enumeration identifying the type of feature.  
2. Populating the properties of this FeatureData object. These properties effectively serve as the parameters for the feature.  
3. Creating the feature by passing the populated FeatureData object to IFeatureManager::CreateFeature(FeatureDataObject As Object).4 This approach offers enhanced clarity and maintainability over older methods that often required an extensive list of parameters in a single function call.

Furthermore, a nearly universal prerequisite for many feature and sketch creation APIs is the pre-selection of geometric entities (e.g., sketches, faces, edges, planes). This is typically accomplished using methods like IModelDocExtension::SelectByID2 or IModelDocExtension::SelectByRay. The Mark parameter within SelectByID2 is particularly important, as its value differentiates the role of the selected entity (e.g., a profile, a path, an axis of revolution) for the subsequent API call.6

### **2.1. Feature Creation APIs (IFeatureManager and related FeatureData objects)**

The IFeatureManager interface is central to creating and managing features within a SolidWorks part or assembly document.

#### **2.1.1. Extrusion (Boss/Base, Cut, Thin)**

Extrusion features are fundamental in 3D modeling, adding or removing material along a specified path.

* **Method: IFeatureManager::FeatureExtrusion3** This method provides a direct means to create boss/base or cut extrusions with various end conditions. *Source parameters based on.7*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Sd | System.Boolean | True for single-ended extrusion, False for double-ended. |
| Flip | System.Boolean | True to flip the side to cut (for cut extrusions). |
| Dir | System.Boolean | True to flip the default direction of extrusion. |
| T1 | System.Integer | Termination type for the first end, as defined in swEndConditions\_e (e.g., swEndCondBlind, swEndCondOffsetFromSurface). |
| T2 | System.Integer | Termination type for the second end (if Sd is False), as defined in swEndConditions\_e. |
| D1 | System.Double | Depth of extrusion for the first end (in meters). If T1 is swEndCondOffsetFromSurface, this is the offset distance. |
| D2 | System.Double | Depth of extrusion for the second end (in meters, if Sd is False). If T2 is swEndCondOffsetFromSurface, this is the offset distance. |
| Dchk1 | System.Boolean | True to enable draft in the first direction. |
| Dchk2 | System.Boolean | True to enable draft in the second direction. |
| Ddir1 | System.Boolean | True for inward draft in the first direction, False for outward (valid if Dchk1 is True). |
| Ddir2 | System.Boolean | True for inward draft in the second direction, False for outward (valid if Dchk2 is True). |
| Dang1 | System.Double | Draft angle for the first end (in radians, valid if Dchk1 is True). |
| Dang2 | System.Double | Draft angle for the second end (in radians, valid if Dchk2 is True). |
| OffsetReverse1 | System.Boolean | True to reverse offset direction for T1 \= swEndCondOffsetFromSurface. |
| OffsetReverse2 | System.Boolean | True to reverse offset direction for T2 \= swEndCondOffsetFromSurface. |
| TranslateSurface1 | System.Boolean | True if first end is a translation of reference surface (for T1 \= swEndCondOffsetFromSurface), False for true offset. |
| TranslateSurface2 | System.Boolean | True if second end is a translation of reference surface (for T2 \= swEndCondOffsetFromSurface), False for true offset. |
| Merge | System.Boolean | True to merge results in a multibody part. |
| UseFeatScope | System.Boolean | True if feature affects selected bodies only, False for all bodies. |
| UseAutoSelect | System.Boolean | True to automatically select all bodies, False to use pre-selected bodies. |
| T0 | System.Integer | Start condition as defined in swStartConditions\_e (e.g., swStartSketchPlane, swStartOffset). |
| StartOffset | System.Double | Offset distance from sketch plane if T0 is swStartOffset (in meters). |
| FlipStartOffset | System.Boolean | True to flip the direction of StartOffset (if T0 is swStartOffset). |

\*Remarks\*: Requires pre-selection of a sketch (Mark 0), and potentially entities for direction (Mark 16), start condition reference (Mark 32), and end condition reference (Mark 1\) using \`IModelDocExtension::SelectByID2\`.\[7\]

* **Method: IFeatureManager::FeatureCut3** This method is analogous to FeatureExtrusion3 but is specifically for creating cut features. *Source parameters based on.75*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Sd | System.Boolean | True for single-ended cut, False for double-ended. |
| Flip | System.Boolean | True to flip the side to cut (remove material outside the profile). |
| Dir | System.Boolean | True to flip the default direction of cut. |
| T1 | System.Integer | Termination type for the first end, as defined in swEndConditions\_e. |
| T2 | System.Integer | Termination type for the second end (if Sd is False), as defined in swEndConditions\_e. |
| D1 | System.Double | Depth of cut for the first end (in meters). If T1 is swEndCondOffsetFromSurface, this is the offset distance. |
| D2 | System.Double | Depth of cut for the second end (in meters, if Sd is False). If T2 is swEndCondOffsetFromSurface, this is the offset distance. |
| Dchk1 | System.Boolean | True to enable draft in the first direction. |
| Dchk2 | System.Boolean | True to enable draft in the second direction. |
| Ddir1 | System.Boolean | True for inward draft in the first direction, False for outward (valid if Dchk1 is True). |
| Ddir2 | System.Boolean | True for inward draft in the second direction, False for outward (valid if Dchk2 is True). |
| Dang1 | System.Double | Draft angle for the first end (in radians, valid if Dchk1 is True). |
| Dang2 | System.Double | Draft angle for the second end (in radians, valid if Dchk2 is True). |
| OffsetReverse1 | System.Boolean | True to reverse offset direction for T1 \= swEndCondOffsetFromSurface. |
| OffsetReverse2 | System.Boolean | True to reverse offset direction for T2 \= swEndCondOffsetFromSurface. |
| TranslateSurface1 | System.Boolean | True if first end is a translation of reference surface (for T1 \= swEndCondOffsetFromSurface), False for true offset. |
| TranslateSurface2 | System.Boolean | True if second end is a translation of reference surface (for T2 \= swEndCondOffsetFromSurface), False for true offset. |
| NormalCut | System.Boolean | True to ensure cut is normal to sheet metal thickness (sheet metal parts only). |
| UseFeatScope | System.Boolean | True if feature affects selected bodies/components only. |
| UseAutoSelect | System.Boolean | True to automatically select all bodies/components. |
| AssemblyFeatureScope | System.Boolean | True if assembly feature affects selected components only. |
| AutoSelectComponents | System.Boolean | True to auto-select all affected components in an assembly. |
| PropagateFeatureToParts | System.Boolean | True to propagate assembly feature to affected parts. |

\*Remarks\*: Selection requirements are similar to \`FeatureExtrusion3\`. The \`NormalCut\` parameter is specific to sheet metal applications.

* **Method: IFeatureManager::FeatureExtrusionThin2** This method is used for creating thin-walled extrusions (boss or cut). *Source parameters based on.11*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Sd | System.Boolean | True for single-ended, False for double-ended. |
| Flip | System.Boolean | Not used. |
| Dir | System.Boolean | True to reverse Direction 1 from default. |
| T1 | System.Integer | Termination type for first end (swEndConditions\_e). |
| T2 | System.Integer | Termination type for second end (swEndConditions\_e). |
| D1 | System.Double | Depth for first end (meters). |
| D2 | System.Double | Depth for second end (meters). |
| Dchk1 | System.Boolean | True for draft in first direction. |
| Dchk2 | System.Boolean | True for draft in second direction. |
| Ddir1 | System.Boolean | True for first draft inward. |
| Ddir2 | System.Boolean | True for second draft inward. |
| Dang1 | System.Double | First draft angle (radians). |
| Dang2 | System.Double | Second draft angle (radians). |
| OffsetReverse1 | System.Boolean | True to reverse offset from surface (Direction 1). |
| OffsetReverse2 | System.Boolean | True to reverse offset from surface (Direction 2). |
| TranslateSurface1 | System.Boolean | True for translation of reference surface (Direction 1). |
| TranslateSurface2 | System.Boolean | True for translation of reference surface (Direction 2). |
| Merge | System.Boolean | True to merge resultant body. |
| Thk1 | System.Double | Wall thickness 1 (meters). For Mid-plane type, uses Thk1/2 for each direction. |
| Thk2 | System.Double | Wall thickness 2 (meters). Used only if RevThinDir \= 3 (Two direction). |
| EndThk | System.Double | End cap thickness (meters). Used only if CapEnds \= 1\. |
| RevThinDir | System.Integer | Thin feature type: 0=OneDir, 1=OneDirReverse, 2=MidPlane, 3=TwoDir. |
| CapEnds | System.Integer | Cap ends: 0=NoCap, 1=Cap (base features only). |
| AddBends | System.Boolean | True to add auto bends (open profile base features only). |
| BendRad | System.Double | Fillet radii if AddBends is True (meters). |
| UseFeatScope | System.Boolean | True if feature affects selected bodies only. |
| UseAutoSelect | System.Boolean | True to automatically select all bodies. |
| T0 | System.Integer | Start condition (swStartConditions\_e). |
| StartOffset | System.Double | Offset value if T0 is swStartOffset (meters). |
| FlipStartOffset | System.Boolean | True to flip start offset direction if T0 is swStartOffset. |

\*Remarks\*: Default extrusion directions depend on the operation type (cut vs. boss) and sketch normal.\[11\]

* **Alternative using FeatureData Object for Extrusions**: The modern approach for creating features, including extrusions, often involves the IFeatureManager::CreateDefinition and IFeatureManager::CreateFeature pattern. For extrusions, this would utilize the IExtrudeFeatureData2 interface.12 The process involves:  
  1. Calling IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmBoss) (or swFmCut, swFmThinBoss, etc.) to obtain an IExtrudeFeatureData2 object.  
  2. Setting the numerous properties on the IExtrudeFeatureData2 object to define the extrusion's parameters (e.g., depth, direction, end conditions, draft, thin feature options).  
  3. Calling IFeatureManager::CreateFeature(extrudeFeatureDataObject) to create the feature. While direct methods like FeatureExtrusion3 are available, the FeatureData approach provides a more object-oriented and potentially more extensible way to define feature parameters, aligning with the evolution of the SolidWorks API. Detailed properties for IExtrudeFeatureData2 were not available in the provided documentation snippets but would be essential for this approach.

#### **2.1.2. Fillet**

Fillets create rounded internal or external edges.

* **Method: IFeatureManager::FeatureFillet3** This method can create various fillet types, including variable radius fillets. However, it is noted as obsolete for constant radius fillets, offset face chamfers, face fillets, face-face chamfers, full round fillets, and partial fillets/chamfers as of SolidWorks 2020\. For these, the CreateDefinition/CreateFeature pattern with ISimpleFilletFeatureData2 is recommended.13 *Source parameters based on.13*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Options | System.Integer | Feature fillet options as defined in swFeatureFilletOptions\_e (e.g., swFeatureFilletUniformRadius, swFeatureFilletAsymmetric, swFeatureFilletVarRadiusType). |
| R1 | System.Double | Uniform radius (if Ftyp\!= swFeatureFilletType\_VariableRadius and Options includes swFeatureFilletUniformRadius). Distance 1 radius for asymmetric fillets. |
| R2 | System.Double | Distance 2 radius for asymmetric fillets (if Ftyp\!= swFeatureFilletType\_VariableRadius and Options includes swFeatureFilletAsymmetric). |
| Rho | System.Double | Conic rho value \[0.05, 0.95\] or conic radius, if ConicRhoType is set accordingly (if Ftyp\!= swFeatureFilletType\_VariableRadius). |
| Ftyp | System.Integer | Type of fillet as defined in swFeatureFilletType\_e (e.g., swFeatureFilletType\_ConstantRadius, swFeatureFilletType\_VariableRadius). |
| OverflowType | System.Integer | Control of fillet overflowing onto adjacent surfaces, as defined in swFilletOverFlowType\_e. |
| ConicRhoType | System.Integer | Fillet cross-section profile (swFeatureFilletProfileType\_e), e.g., swFeatureFilletConicRho, swFeatureFilletConicRadius. Valid if not curvature continuous. |
| Radii | System.Object | Array of radii for symmetric variable radius fillet, or Distance 1 radii for asymmetric variable radius fillet. Valid if Ftyp \= swFeatureFilletType\_VariableRadius. |
| Dist2Arr | System.Object | Array of Distance 2 radii for asymmetric variable radius fillet. Valid if Ftyp \= swFeatureFilletType\_VariableRadius and asymmetric option. |
| RhoArr | System.Object | Array of conic rho/radius values for variable radius fillet. Valid if Ftyp \= swFeatureFilletType\_VariableRadius. |
| SetBackDistances | System.Object | Array assigning setback distances on edges meeting at a selected fillet corner. |
| PointRadiusArray | System.Object | Array of control point radii (symmetric) or Distance 1 control point radii (asymmetric) for variable radius fillet with control points. |
| PointDist2Array | System.Object | Array of Distance 2 control point radii for asymmetric variable radius fillet with control points. |
| PointRhoArray | System.Object | Array of conic rho/radius values at control points for variable radius fillet. |

\*Remarks\*: Careful population and ordering of arrays are crucial. Invalid conic rho values are adjusted to the nearest valid range (0.05 or 0.95).\[13\] Pre-selection of edges, faces, or vertices is required.

* **Modern Approach for Fillets/Chamfers using FeatureData**: The recommended method for creating many fillet and chamfer types involves IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmFillet) (or swFmChamfer), populating an ISimpleFilletFeatureData2 object, and then calling IFeatureManager::CreateFeature.4 The ISimpleFilletFeatureData2 interface is versatile, handling various fillet and chamfer scenarios by adjusting its properties. *Key Properties of ISimpleFilletFeatureData2 (act as parameters for CreateFeature):* *Source properties based on.14*

| Property | Data Type | Description |
| :---- | :---- | :---- |
| Type | System.Integer | Type of feature (e.g., swSimpleFilletType\_e values for constant radius, variable radius, face fillet, chamfer). |
| AsymmetricFillet | System.Boolean | True if the fillet/chamfer is asymmetric. |
| DefaultRadius | System.Double | Default radius for the fillet (meters). |
| DefaultDistance | System.Double | Default Distance 2 radius for an asymmetric fillet (meters). |
| ConstantWidth | System.Boolean | True if the simple fillet has a constant width.14 |
| Edges | System.Object | Array of edges to apply the fillet/chamfer to. |
| Faces | System.Object | Array of faces (for face fillets/chamfers). Set via SetFaces. |
| Loops | System.Object | Array of loops to apply the fillet to. |
| Features | System.Object | Array of features whose edges are to be filleted/chamfered. |
| PropagateToTangentFaces | System.Boolean | True to extend the fillet/chamfer to all tangent faces. |
| CurvatureContinuous | System.Boolean | True for a smoother curvature continuous fillet. |
| ConicTypeForCrossSectionProfile | System.Integer | Cross-sectional profile type (swFeatureFilletProfileType\_e) for conic fillets (e.g., swFeatureFilletConicRho, swFeatureFilletConicRadius). |
| DefaultConicRhoOrRadius | System.Double | Default conic rho value (0.05-0.95) or conic radius, depending on ConicTypeForCrossSectionProfile. |
| OverflowType | System.Integer | How the fillet handles overflow conditions (swFilletOverFlowType\_e). |
| RoundCorners | System.Boolean | True to round sharp corners created by the fillet. |
| KeepFeatures | System.Boolean | True to attempt to keep existing features on filleted entities. |
| HoldLines | System.Object | Array of hold lines (boundaries) for a face blend fillet feature. |
| IsMultipleRadius | System.Boolean | True if a symmetric fillet or chamfer has multiple radius values defined per edge/vertex. |

\*Key Methods\*: \`Initialize\` (typically called after \`CreateDefinition\`), \`AccessSelections\`, \`ReleaseSelectionAccess\`, \`SetFaces\`, \`SetConicRhoOrRadius(PFilletItem As Object, ConicRhoVal As Double)\`.\[16\]  
This structured approach using \`ISimpleFilletFeatureData2\` is central to modern SolidWorks API programming for fillets and chamfers, offering a clearer and more robust way to define these features compared to older, parameter-heavy methods.

#### **2.1.3. Revolve (Boss/Base, Cut)**

Revolve features create geometry by rotating a sketch profile around an axis.

* **Method: IFeatureManager::FeatureRevolve2** This method creates revolved boss/base or cut features. The documentation for this method appears to be from an older API version (SolidWorks 2011\) but provides named parameters.8 *Source parameters based on.8*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| SingleDir | System.Boolean | True if the revolve is in one direction, False if in two directions. |
| IsSolid | System.Boolean | True if this is a solid revolve feature, False if a surface revolve. |
| IsThin | System.Boolean | True if this is a thin revolve feature. |
| IsCut | System.Boolean | True if this is a cut revolve feature. |
| ReverseDir | System.Boolean | True to reverse the primary angle of revolution. |
| BothDirectionUpToSameEntity | System.Boolean | If SingleDir is False and revolve is up to/offset from the same entity in both directions, set to True and select entity once. |
| Dir1Type | System.Integer | Revolve end condition in direction 1 (e.g., 0=Blind, 3=UpToVertex, 4=UpToSurface, 5=OffsetFromSurface, 6=MidPlane \- likely an enum like swEndConditions\_e). |
| Dir2Type | System.Integer | Revolve end condition in direction 2 (if SingleDir is False). |
| Dir1Angle | System.Double | Angle of revolution in direction 1 (radians); applies if Dir1Type is Blind. |
| Dir2Angle | System.Double | Angle of revolution in direction 2 (radians); applies if Dir2Type is Blind and SingleDir is False. |
| OffsetReverse1 | System.Boolean | True to reverse offset direction in direction 1; applies if Dir1Type is OffsetFromSurface. |
| OffsetReverse2 | System.Boolean | True to reverse offset direction in direction 2; applies if Dir2Type is OffsetFromSurface. |
| OffsetDistance1 | System.Double | Offset distance in direction 1 (meters); applies if Dir1Type is OffsetFromSurface. |
| OffsetDistance2 | System.Double | Offset distance in direction 2 (meters); applies if Dir2Type is OffsetFromSurface. |
| ThinType | System.Integer | Type of thin feature (e.g., 0=OneDirection, 1=MidPlane, 2=TwoDirection \- likely an enum like swThinWallType\_e). |
| ThinThickness1 | System.Double | Thickness for thin feature in primary direction (meters). |
| ThinThickness2 | System.Double | Thickness for thin feature in secondary direction (meters, if ThinType is TwoDirection). |

\*Remarks\*: Requires pre-selection of the sketch profile, axis of revolution (Mark 16), and any up-to/offset-from entities using \`IModelDocExtension::SelectByID2\`.\[8\]

* **Modern Approach for Revolves using FeatureData**: The contemporary method involves IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmRevolve) (or swFmRevolveCut), configuring an IRevolveFeatureData2 object, and then calling IFeatureManager::CreateFeature. *Key Properties/Methods of IRevolveFeatureData2 (act as parameters for CreateFeature):* *Source properties/methods based on.77*

| Member Name | Type | Description |
| :---- | :---- | :---- |
| Type | Property | Revolution feature type (e.g., swRevolveType\_e: Boss/Base, Cut, Surface). |
| Axis | Property | The axis of revolution (selected sketch line or edge). |
| Contours | Property | Selected sketch contours for revolution. Set via ISetContours. |
| IsBossFeature | Method | Gets whether the revolution is a boss feature. |
| IsThinFeature | Method | Gets whether the revolution is a thin feature. |
| ThinWallType | Property | Type of thin wall (e.g., swThinWallType\_e). |
| ReverseDirection | Property | True to reverse the direction of revolution. |
| Merge | Property | True to merge results in a multibody part. |
| FeatureScope | Property | True to use scope for the revolve feature in a multibody part. |
| FeatureScopeBodies | Property | Solid bodies affected by the revolve feature in a multibody part. Set via ISetFeatureScopeBodies. |
| AssemblyFeatureScope | Property | True to use scope for this assembly revolve feature. |
| AutoSelectComponents | Property | True to auto-select all components affected by this assembly revolve feature. |
| PropagateFeatureToParts | Property | True to propagate this assembly revolve feature to the parts it affects. |
| SetRevolutionAngle | Method | Sets the angle of the revolve feature in Direction 1 or Direction 2 (Parameters: Direction as Integer, Angle as Double). |
| SetWallThickness | Method | Sets the wall thickness of the thin revolution feature in forward/reverse direction (Parameters: Direction as Integer, Thickness as Double). |
| AccessSelections | Method | Gains access to the selections that define this revolve feature. |
| ReleaseSelectionAccess | Method | Releases access to the selections. |

This \`FeatureData\` approach provides a more granular and object-oriented way to define revolve parameters before creation.

#### **2.1.4. Sweep (Boss/Base, Cut, Surface)**

Sweep features create geometry by moving a profile along a path.

* **Modern Approach using FeatureData**: Sweep creation predominantly uses the IFeatureManager::CreateDefinition and IFeatureManager::CreateFeature pattern. The ISweepFeatureData interface holds the extensive options for sweeps.9 The process is:  
  1. Pre-select entities: Profile (Mark 1 for sketch/face, Mark 4 for circular, Mark 1 & 2048 for solid cut), Path (Mark 4), Guide Curves (Mark 2, optional), Direction Vector (Mark 128, optional) using IModelDocExtension::SelectByRay or SelectByID2.9  
  2. Call IFeatureManager::CreateDefinition(swFeatureNameID\_e) with swFmSweep (boss), swFmSweepCut (cut), or swFmRefSurface (surface) to get an ISweepFeatureData object.  
  3. Set properties on the ISweepFeatureData object.  
  4. Call IFeatureManager::CreateFeature(sweepFeatureDataObject). *Key Properties/Methods of ISweepFeatureData (act as parameters for CreateFeature):* *Source properties/methods based on.9*

| Property | Data Type | Description |
| :---- | :---- | :---- |
| Profile | System.Object | The sketch profile or tool body. |
| Path | System.Object | The sweep path. |
| GuideCurves | System.Object | Array of guide curves. |
| SweepType | System.Integer | Type of sweep (e.g., swSweepOutputType: Solid, Surface, Cut). |
| PathAlignmentType | System.Integer | Alignment of the profile to the path (e.g., swTangencyType\_e: swTangencyNone, swTangencyDirectionVector). |
| TwistControlType | System.Integer | Type of twist control (e.g., swTwistControlType\_e: swTwistControlFollowPath, swTwistControlConstantTwistAlongPath).17 |
| Direction | System.Integer | Direction type of the sweep (e.g., swSweepDirection\_e: Unidirectional, Bidirectional). |
| ThinFeature | System.Boolean | True to make a thin-walled sweep. |
| ThinWallType | System.Integer | Type of thin wall (e.g., swThinWallType\_e: OneDirection, MidPlane, TwoDirection). |
| SetWallThickness | Method | Sets wall thickness(es) for thin feature (Parameters: Thickness1 As Double, Thickness2 As Double). |
| AlignWithEndFaces | System.Boolean | True to align sweep with end faces.18 Must be False if TwistControlType is swTwistControlConstantTwistAlongPath. |
| MaintainTangency | System.Boolean | True to merge tangent faces. |
| TangentPropagation | System.Boolean | True to propagate sweep to the next tangent edge. |
| Merge | System.Boolean | True to merge results of swept-boss for multibody part. |
| MergeSmoothFaces | System.Boolean | True to merge smooth faces if using guide curves. |
| CircularProfile | System.Boolean | True to use a circular profile. |
| CircularProfileDiameter | System.Double | Diameter for circular profile (meters). |
| StartTangencyType | System.Integer | Tangency at the start of the sweep path (swTangencyType\_e). |
| EndTangencyType | System.Integer | Tangency at the end of the sweep path (swTangencyType\_e). |
| AdvancedSmoothing | System.Boolean | True to apply advanced smoothing. |
| FeatureScope | System.Boolean | True to use scope in a multibody part. |
| FeatureScopeBodies | System.Object | Array of solid bodies affected in a multibody part. |
| AssemblyFeatureScope | System.Boolean | True if swept-cut affects only selected assembly components.19 |
| AutoSelectComponents | System.Boolean | True to auto-select all affected assembly components for swept-cut.19 |
| PropagateFeatureToParts | System.Boolean | True to extend swept-cut to all affected parts in an assembly. |

\*Remarks\*: The complexity of sweep features, with their numerous options for profile orientation, twist control, and guide curve influence, makes the \`ISweepFeatureData\` object essential for precise definition prior to calling \`IFeatureManager::CreateFeature\`.

#### **2.1.5. Loft (Boss/Base, Cut)**

Loft features create geometry by transitioning between two or more profiles.

* **Method: IFeatureManager::InsertProtrusionBlend2** (for lofted boss/base) This method creates a lofted boss/base feature.10 A corresponding method for lofted cuts (e.g., InsertCutBlend2) would be expected but is not explicitly detailed for IFeatureManager in the provided snippets. *Source parameters based on.10*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Closed | System.Boolean | True closes the loft. If True and \< 3 profiles, guide curves must be closed. |
| KeepTangency | System.Boolean | True maintains tangency from section curves. |
| ForceNonRational | System.Boolean | True for smoother surfaces. |
| TessToleranceFactor | System.Double | Controls intermediate sections for loft with centerline (default 1.0). |
| StartMatchingType | System.Short | Tangency type at start profile (0=None, 1=NormalToProfile, 2=SelectedVector, 3=AllAdjacent). |
| EndMatchingType | System.Short | Tangency type at end profile (0=None, 1=NormalToProfile, 2=SelectedVector, 3=AllAdjacent). |
| StartTangentLength | System.Double | Start tangent length (meters). |
| EndTangentLength | System.Double | End tangent length (meters). |
| StartTangentDir | System.Boolean | Direction of start tangent (True for one direction, False for opposite). |
| EndTangentDir | System.Boolean | Direction of end tangent (True for one direction, False for opposite). |
| IsThinBody | System.Boolean | True if this is a thin body feature. |
| Thickness1 | System.Double | Thickness for first direction of thin body (meters). |
| Thickness2 | System.Double | Thickness for second direction of thin body (meters). |
| ThinType | System.Short | Thin wall type (0=OneDir, 1=OneDirReverse, 2=MidPlane, 3=TwoDir). |
| Merge | System.Boolean | True to merge results in a multibody part. |
| UseFeatScope | System.Boolean | True if feature affects selected bodies only. |
| UseAutoSelect | System.Boolean | True to auto-select all bodies. |
| GuideCurveInfluence | System.Integer | How guide curves influence the loft, as per swGuideCurveInfluence\_e. |

\*Remarks\*: Requires pre-selection of profiles (Mark 1), guide curves (Mark 2), centerline (Mark 4), and tangency vectors (Mark 8 for start, Mark 32 for end) using \`IModelDocExtension::SelectByID2\`.\[10\]

* Alternative using IModeler:  
  The IModeler::CreateLoftBody method can also create lofted geometry.20 However, this is a lower-level API call that returns a Body2 object rather than a Feature object. Integrating this body into the feature tree would require additional steps by the developer. For feature-level automation, IFeatureManager methods are generally preferred.  
* Modern Approach for Lofts using FeatureData:  
  The CreateDefinition/CreateFeature pattern likely applies, using IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmBlend) (or swFmBlendCut) and an ILoftFeatureData object.12 Properties for ILoftFeatureData were not detailed in the provided snippets.

#### **2.1.6. Chamfer**

Chamfers create beveled edges or vertices.

* **Method: IFeatureManager::InsertFeatureChamfer** This method creates chamfer features. *Source parameters based on.21*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Options | System.Integer | Options as defined by swFeatureChamferOption\_e. |
| ChamferType | System.Integer | Chamfer type as defined by swChamferType\_e (e.g., swChamferAngleDistance, swChamferDistanceDistance, swChamferVertex). |
| Width | System.Double | Width of chamfer (meters), if ChamferType is swChamferAngleDistance. |
| Angle | System.Double | Angle of chamfer (radians), if ChamferType is swChamferAngleDistance. |
| OtherDist | System.Double | Single distance value (meters) for equal distance chamfer, if ChamferType is swChamferEqualDistance. |
| VertexChamDist1 | System.Double | Distance on first side (meters), if ChamferType is swChamferDistanceDistance or swChamferVertex. |
| VertexChamDist2 | System.Double | Distance on second side (meters), if ChamferType is swChamferDistanceDistance or swChamferVertex. |
| VertexChamDist3 | System.Double | Distance on third side (meters), if ChamferType is swChamferVertex. |

\*Remarks\*: \`swChamferAngleDistance\` and \`swChamferDistanceDistance\` are edge chamfers. \`swChamferVertex\` applies to a vertex with three adjacent edges of the same convexity.\[21\] Requires pre-selection of edges or vertices.

* **Modern Approach for Chamfers using FeatureData**: Similar to fillets, the recommended approach for chamfers is IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmChamfer), populating an ISimpleFilletFeatureData2 object (as it handles both fillets and chamfers), and then calling IFeatureManager::CreateFeature.13 The Type property and other relevant settings within ISimpleFilletFeatureData2 would distinguish it as a chamfer. Refer to the ISimpleFilletFeatureData2 properties table in Section 2.1.2.

#### **2.1.7. Shell**

Shell features hollow out a part, leaving a specified wall thickness.

* **Modern Approach using FeatureData**: Creating shell features programmatically involves IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmShell), configuring the properties of an IShellFeatureData object, and then calling IFeatureManager::CreateFeature. *Key Properties/Methods of IShellFeatureData (act as parameters for CreateFeature):* *Source methods from.79 Properties are deduced or commonly expected for shell features.*

| Member Name | Type | Description |
| :---- | :---- | :---- |
| DefaultThickness | Property | (Expected) Default wall thickness for the shell (System.Double, meters). |
| ShellDirectionOutward | Property | (Expected) True to shell outward, False to shell inward (System.Boolean). |
| ISetFacesRemoved | Method | Sets the faces to be removed from the part (Parameter: Faces As System.Object \- array of face objects). |
| IGetFacesRemoved | Method | Gets the faces removed in this shell feature. |
| ISetMultipleThicknessFaces | Method | Sets faces that will have a different thickness from the default (Parameter: Faces As System.Object). |
| IGetMultipleThicknessFaces | Method | Gets the multiple-thickness faces in this shell feature. |
| SetMultipleThicknessAtIndex | Method | Sets the thickness for a specific face that has a non-default thickness (Parameters: Index As System.Integer, Thickness As System.Double). |
| GetMultipleThicknessAtIndex | Method | Gets the thickness of the shell at the specified index. |
| GetMultipleThicknessFacesCount | Method | Gets the number of faces with multiple thicknesses. |
| AccessSelections | Method | Gains access to the selections that define this shell feature. |
| ReleaseSelectionAccess | Method | Releases access to the selections. |

This \`FeatureData\` approach allows for detailed specification of faces to remove and varying wall thicknesses, which are common requirements for shell features.

#### **2.1.8. Draft**

Draft features apply an angle to specified faces, typically for molding purposes.

* **Modern Approach using FeatureData**: Draft features are created using IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmDraft), setting properties on an IDraftFeatureData2 object, and then calling IFeatureManager::CreateFeature. The IDraftFeatureData interface is obsolete and superseded by IDraftFeatureData2.22 *Key Properties of IDraftFeatureData2 (act as parameters for CreateFeature):* *Source properties based on 23 and common draft parameters.*

| Property | Data Type | Description |
| :---- | :---- | :---- |
| FacesToDraft | System.Object | Array of faces to apply the draft to.23 |
| DraftAngle | System.Double | (Expected) The angle of the draft (radians). |
| NeutralPlane | System.Object | (Expected) The plane or face from which the draft is measured (for Neutral Plane draft). |
| PullDirection | System.Object | (Expected) The direction vector or entity defining the pull direction. |
| DraftType | System.Integer | (Expected) Type of draft (e.g., swDraftType\_e: Neutral Plane, Parting Line, Step Draft). |
| PartingLine | System.Object | (Expected) Selected edges or sketch defining the parting line (for Parting Line draft). |
| StepDraftFaces | System.Object | (Expected) Array of faces for step draft, with associated step directions. |

\*Remarks\*: Pre-selection of the neutral plane/parting line and faces to draft is typically required.

#### **2.1.9. Rib**

Rib features add thin structural supports to parts.

* **Modern Approach using FeatureData**: Ribs are created via IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmRib), configuring an IRibFeatureData2 object, and then calling IFeatureManager::CreateFeature. The IFeatureManager::InsertRib method is also mentioned in relation to IRibFeatureData2.24 *Properties of IRibFeatureData2 (act as parameters for CreateFeature):* *Source properties based on.24*

| Property | Data Type | Description |
| :---- | :---- | :---- |
| Type | System.Integer | Type of rib (e.g., swRibType\_e: Parallel to Sketch, Normal to Sketch). |
| Thickness | System.Double | Overall thickness of the rib (meters). |
| ExtrusionDirection | System.Integer | Direction in which to extrude the rib (e.g., swRibExtrusionDirection\_e: Both Sides, First Side, Second Side from sketch plane). |
| FlipSide | System.Boolean | True if material is added to the reverse side of the rib sketch plane. |
| IsTwoSided | System.Boolean | True if the rib is created on two sides of the midplane (symmetric thickness), False for single direction (see ReverseThicknessDir). |
| ReverseThicknessDir | System.Boolean | True if the extrusion is on the reverse side of this single-sided rib (relative to sketch). |
| EnableDraft | System.Boolean | True if the rib has an associated draft. |
| DraftAngle | System.Double | Draft angle for the rib (radians), if EnableDraft is True. |
| DraftOutward | System.Boolean | True if the rib has an outward draft, False for inward. |
| DraftFromWall | System.Boolean | True to draft the rib from the wall interface, False from the sketch plane. |
| Body | System.Object | The body where the rib is created (relevant in multibody parts). |
| RefSketchIndex | System.Integer | Index of the sketch segment defining the draft direction of the rib feature (if draft direction is tied to a sketch segment). |

\*Remarks\*: Requires a pre-selected open or closed sketch profile. The \`IRibFeatureData2\` interface provides a structured method to define the rib's geometric parameters and options before its creation.

#### **2.1.10. Hole (Simple and Wizard)**

Holes can be simple extruded cuts or complex, standardized holes created using the Hole Wizard.

* **Simple Hole**: Typically created as an extruded cut feature. Refer to Section 2.1.1 Extrusion (Cut).  
* Method: IFeatureManager::HoleWizard5  
  This method creates Hole Wizard features, supporting a wide array of standards and types. It is a complex method due to its versatility.  
  Source parameters based on.25

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| GenericHoleType | System.Integer | Type of hole/slot (swWzdGeneralHoleTypes\_e, e.g., swWzdHole, swWzdCounterBore, swWzdTap). |
| StandardIndex | System.Integer | Hole/slot standard (swWzdHoleStandards\_e, e.g., swStdAnsiInch, swStdIso). |
| FastenerTypeIndex | System.Integer | Fastener type within the standard (swWzdHoleStandardFastenerTypes\_e). Must match standard and hole type. |
| SSize | System.String | Size of the hole/slot (e.g., "1/4-20", "M6"). |
| EndType | System.Short | Hole/slot end condition (swEndConditions\_e, e.g., swEndCondBlind, swEndCondThroughAll). |
| Diameter | System.Double | Diameter of the hole/slot (meters). |
| Depth | System.Double | Depth of the hole/slot (meters). |
| Length | System.Double | Length of slot (meters); valid for slot types (swWzdCounterBoreSlot, etc.). |
| Value1... Value12 | System.Double | Hole/slot parameters (e.g., C'Bore Dia, C'Sink Angle). Meaning depends on GenericHoleType. Ignored if \-1. See 25 remarks for detailed mapping for various hole types. |
| ThreadClass | System.String | Thread class (e.g., "1B", "2B", "3B"). ANSI inch standard only. |
| RevDir | System.Boolean | True to reverse hole/slot direction. |
| FeatureScope | System.Boolean | True if feature affects selected bodies only. |
| AutoSelect | System.Boolean | True to auto-select all bodies. |
| AssemblyFeatureScope | System.Boolean | True if assembly feature affects selected components only. |
| AutoSelectComponents | System.Boolean | True to auto-select all affected components in assembly. |
| PropagateFeatureToParts | System.Boolean | True to propagate assembly feature to affected parts. |

\*Remarks\*: Requires pre-selection of sketch points or faces for hole locations. The parameters \`Value1\` through \`Value12\` are critical and their interpretation is highly dependent on the \`GenericHoleType\` chosen, covering aspects like counterbore dimensions, countersink angles, drill angles, thread parameters, etc..\[25\]

* Method: IFeatureManager::AdvancedHole  
  This method is mentioned for creating Advanced Holes composed of stacked hole elements.25 Detailed parameters were not available in the provided snippets.  
* Modern Approach for Hole Wizard using FeatureData:  
  The CreateDefinition/CreateFeature pattern can also be applied to Hole Wizard features using IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmWizardHole), configuring an IWizardHoleFeatureData2 object, and then calling IFeatureManager::CreateFeature. The IWizardHoleFeatureData2 interface has seen updates, such as properties for UnderHeadCounterSink 26, indicating its role in modern API usage for Hole Wizard features. Detailed properties for IWizardHoleFeatureData2 beyond these were not fully available.

#### **2.1.11. Linear Pattern**

Linear patterns create copies of features, faces, or bodies along one or two linear directions.

* **Method: IFeatureManager::FeatureLinearPattern4** This method creates linear pattern features. *Source parameters based on.27*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Num1 | System.Integer | Number of instances in Direction 1 (including original). |
| Spacing1 | System.Double | Spacing between instances in Direction 1 (meters). |
| Num2 | System.Integer | Number of instances in Direction 2 (including original). Set to 0 or 1 if no Direction 2\. |
| Spacing2 | System.Double | Spacing between instances in Direction 2 (meters). |
| FlipDir1 | System.Boolean | True to reverse Direction 1\. |
| FlipDir2 | System.Boolean | True to reverse Direction 2\. |
| DName1 | System.String | Name of dimension defining Direction 1 (if applicable, else pre-select edge/axis). |
| DName2 | System.String | Name of dimension defining Direction 2 (if applicable, else pre-select edge/axis). |
| GeometryPattern | System.Boolean | True to use geometry pattern (faster, less robust for re-solving features). |
| VaryInstance | System.Boolean | True to vary dimensions/spacing of instances (if GeometryPattern is False). Requires prior call to InsertVaryInstanceIncrement or InsertVaryInstanceOverride. |
| HasOffset1 | System.Boolean | True if using Offset1 from a reference in Direction 1\. |
| HasOffset2 | System.Boolean | True if using Offset2 from a reference in Direction 2\. |
| CtrlByNum1 | System.Boolean | True to control spacing by Num1 (up to reference), False by Spacing1 (if HasOffset1 is True). |
| CtrlByNum2 | System.Boolean | True to control spacing by Num2 (up to reference), False by Spacing2 (if HasOffset2 is True). |
| FromCentroid1 | System.Boolean | True if Offset1 is from centroid of seed, False from selected reference on seed (if HasOffset1 is True). |
| FromCentroid2 | System.Boolean | True if Offset2 is from centroid of seed, False from selected reference on seed (if HasOffset2 is True). |
| RevOffset1 | System.Boolean | True to reverse direction of Offset1 (if HasOffset1 is True). |
| RevOffset2 | System.Boolean | True to reverse direction of Offset2 (if HasOffset2 is True). |
| Offset1 | System.Double | Offset from reference in Direction 1 (meters, if HasOffset1 is True). |
| Offset2 | System.Double | Offset from reference in Direction 2 (meters, if HasOffset2 is True). |

\*Remarks\*: Requires pre-selection of Direction 1 axis/edge (Mark 1), Direction 2 axis/edge (Mark 2), features to pattern (Mark 4), and optionally offset references (Mark 2097152\) or seed instance reference (Mark 8388608).\[27\] For component patterns, marks are different.

* **Modern Approach for Linear Patterns using FeatureData**: The CreateDefinition/CreateFeature pattern with IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmLinearPattern) and an ILinearPatternFeatureData object provides a more structured way to define linear patterns. *Key Properties/Methods of ILinearPatternFeatureData (act as parameters for CreateFeature):* *Source properties/methods based on.28*

| Member Name | Type | Description |
| :---- | :---- | :---- |
| D1Axis | Property | Entity defining Direction 1 (edge, axis, dimension). |
| D1Spacing | Property | Spacing between instances in Direction 1 (meters). |
| D1TotalInstances | Property | Total number of instances in Direction 1\. |
| D1ReverseDirection | Property | True to reverse Direction 1\. |
| D1EndCondition | Property | End condition for Direction 1 (e.g., swPatternEndCondition\_e: swPatternEndCondition\_SpacingAndInstances, swPatternEndCondition\_UpToReference). |
| D1EndReference | Property | Entity for "Up To Reference" in Direction 1\. |
| D1EndRefOffset | Property | Offset from D1EndReference (meters).28 Valid if D1EndCondition is swPatternEndCondition\_UpToReference. |
| D2Axis | Property | Entity defining Direction 2\. |
| D2Spacing | Property | Spacing between instances in Direction 2 (meters). |
| D2TotalInstances | Property | Total number of instances in Direction 2\. |
| D2ReverseDirection | Property | True to reverse Direction 2\. |
| D2EndCondition | Property | End condition for Direction 2\. |
| D2EndReference | Property | Entity for "Up To Reference" in Direction 2\. |
| D2EndRefOffset | Property | Offset from D2EndReference (meters).28 Valid if D2EndCondition is swPatternEndCondition\_UpToReference. |
| D2PatternSeedOnly | Property | True to pattern only the seed in Direction 2, not instances from Direction 1\. |
| GeometryPattern | Property | True to use geometry pattern. |
| BodyPattern | Property | True to pattern bodies instead of features/faces. |
| PatternBodyArray | Property | Array of bodies to pattern (if BodyPattern is True). |
| PatternFeatureArray | Property | Array of features to pattern.29 |
| SkippedItemArray | Property | Array of instance indices to skip. |
| VarySketch | Property | True to allow pattern instances to vary (if GeometryPattern is False). |
| PropagateVisualProperty | Property | True to propagate visual properties. |
| AccessSelections | Method | Gains access to the selections that define the pattern. |
| ReleaseSelectionAccess | Method | Releases access to the selections. |

This object-oriented approach is crucial for managing the numerous parameters involved in linear patterns effectively.

#### **2.1.12. Circular Pattern**

Circular patterns create copies of features, faces, or bodies around an axis.

* **Method: IFeatureManager::FeatureCircularPattern5** This method creates circular pattern features. *Source parameters based on.30*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Number | System.Integer | Number of instances in Direction 1 (including original). |
| Spacing | System.Double | Spacing angle between instances in Direction 1 (radians), or total angle if EqualSpacing is True. |
| FlipDirection | System.Boolean | True to flip direction of pattern in Direction 1\. |
| DName | System.String | Name of angular dimension defining Direction 1 (if applicable, else pre-select axis). |
| GeometryPattern | System.Boolean | True to use geometry pattern. |
| EqualSpacing | System.Boolean | True for equal spacing over Spacing angle in Direction 1\. |
| VaryInstance | System.Boolean | True to vary instances (if GeometryPattern is False). Requires prior call to InsertVaryInstanceIncrement or InsertVaryInstanceOverride. |
| SyncSubAssemblies | System.Boolean | True to synchronize component movement in patterned flexible subassemblies. |
| BDir2 | System.Boolean | True to enable Direction 2 for bidirectional pattern. |
| BSymmetric | System.Boolean | True for symmetric pattern in Direction 2 (if BDir2 is True). |
| Number2 | System.Integer | Number of instances in Direction 2 (if BDir2 is True). |
| Spacing2 | System.Double | Spacing angle between instances in Direction 2 (radians, if BDir2 is True). |
| DName2 | System.String | Name of angular dimension defining Direction 2 (if BDir2 is True). |
| EqualSpacing2 | System.Boolean | True for equal spacing in Direction 2 (if BDir2 is True and BSymmetric is False). |

\*Remarks\*: Requires pre-selection of the axis of rotation (Mark 1 or 2 depending on context) and features/bodies/components to pattern (Mark 4 or 1).\[30\]

* **Modern Approach for Circular Patterns using FeatureData**: The CreateDefinition/CreateFeature pattern with IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmCircularPattern) and an ICircularPatternFeatureData object is the preferred modern method. *Key Properties/Methods of ICircularPatternFeatureData (act as parameters for CreateFeature):* *Source properties/methods based on.31*

| Member Name | Type | Description |
| :---- | :---- | :---- |
| Axis | Property | Entity defining the axis of rotation. |
| TotalInstances | Property | (Expected for Dir1) Total number of instances in Direction 1\. |
| Angle | Property | (Expected for Dir1) Total angle for pattern (if EqualSpacing is True) or angle between instances (radians) in Direction 1\. |
| EqualSpacing | Property | True for equal spacing in Direction 1\.31 |
| ReverseDirection | Property | (Expected for Dir1) True to reverse Direction 1\. |
| GeometryPattern | Property | True to use geometry pattern. |
| BodyPattern | Property | True to pattern bodies. |
| PatternBodyArray | Property | Array of bodies to pattern (if BodyPattern is True).32 |
| PatternFeatureArray | Property | Array of features to pattern. |
| PatternFaceArray | Property | Array of faces to pattern. Can be set using ISetPatternFaceArray.33 |
| SkippedItemArray | Property | Array of instance indices to skip.34 |
| Direction2Enabled | Property | (Expected) True to enable Direction 2\. |
| Dir2TotalInstances | Property | (Expected) Total number of instances in Direction 2\. |
| Dir2Angle | Property | (Expected) Total angle or instance spacing for Direction 2 (radians). |
| Dir2EqualSpacing | Property | (Expected) True for equal spacing in Direction 2\. |
| Dir2ReverseDirection | Property | (Expected) True to reverse Direction 2\. |
| Symmetric | Property | (Expected) True for symmetric pattern in Direction 2\. |
| AccessSelections | Method | Gains access to the selections that define the pattern. |
| ReleaseSelectionAccess | Method | Releases access to the selections.35 |

This structured object provides better control over the numerous parameters defining a circular pattern.

#### **2.1.13. Mirror**

Mirror features create a mirrored copy of features, faces, or bodies across a plane.

* **Method: IFeatureManager::InsertMirrorFeature2** This method creates mirror features. *Source parameters based on.36*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| BMirrorBody | System.Boolean | True to mirror solid bodies; False to mirror a feature or face. |
| BGeometryPattern | System.Boolean | True to mirror only feature geometry (faster, for features only); False to solve the entire feature. |
| BMerge | System.Boolean | True to merge mirrored solid bodies (if BMirrorBody is True). |
| BKnit | System.Boolean | True to knit mirrored surfaces (if mirroring surfaces). |
| ScopeOptions | System.Integer | Feature scope options as defined in swFeatureScope\_e. |

\*Remarks\*: Requires pre-selection of the mirror plane/face (Mark 2\) and entities to mirror: Features (Mark 1), Faces (Mark 128), Bodies (Mark 256), or Structure Systems (Mark 134217728\) using \`IModelDocExtension::SelectByID2\`.\[36\]

* **Modern Approach for Mirroring using FeatureData**: The CreateDefinition/CreateFeature pattern is used, with different FeatureData objects depending on the entities being mirrored:  
  * For mirroring features/parts: IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmMirrorPart) \+ IMirrorPartFeatureData properties \+ IFeatureManager::CreateFeature.12  
  * For mirroring solid bodies: IFeatureManager::CreateDefinition(swFeatureNameID\_e.swFmMirrorSolid) \+ IMirrorSolidFeatureData properties \+ IFeatureManager::CreateFeature.12 *Key Properties/Methods of IMirrorSolidFeatureData (act as parameters for CreateFeature):* *Source properties/methods based on.37*

| Member Name | Type | Description |
| :---- | :---- | :---- |
| MirrorPlane | Property | (Expected) The plane or planar face to mirror about. |
| SeedBodies | Property | Array of bodies to mirror. Set via ISetPatternBodyArray. |
| Merge | Property | True to merge the new mirrored body/bodies with existing bodies in a multibody part (default is True).37 |
| KnitSurfaces | Property | (Expected, if applicable to surfaces) True to knit mirrored surfaces. |
| AccessSelections | Method | Gains access to the selections that define the mirror solid feature. |
| ReleaseSelectionAccess | Method | Releases access to the selections. |
| IGetPatternBodyArray | Method | Gets the seed bodies for this mirror solid feature. |
| ISetPatternBodyArray | Method | Sets the seed bodies for this mirror solid feature. |

\*(Detailed properties for \`IMirrorPartFeatureData\` were not available in the provided snippets.)\*

### **2.2. Sketching APIs (ISketchManager)**

The ISketchManager interface is used for creating and manipulating 2D and 3D sketch entities. A common performance optimization when creating multiple sketch entities is to use ISketchManager::AddToDB(True) before creation and ISketchManager::DisplayWhenAdded(False) to suppress immediate screen updates, then calling ISketchManager::AddToDB(False) and ISketchManager::DisplayWhenAdded(True) (followed by a screen redraw) upon completion.38 Most creation methods require an active sketch and return a SketchSegment object or a more specific type like SketchPoint or SketchSpline. For 2D sketches, Z-coordinates are often ignored or assumed to be 0\.

#### **2.2.1. Line Creation**

* **Method: ISketchManager::CreateLine** Creates a sketch line segment between two points. *Source parameters based on.44*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| X1 | System.Double | X-coordinate of the line start point (meters). |
| Y1 | System.Double | Y-coordinate of the line start point (meters). |
| Z1 | System.Double | Z-coordinate of the line start point (meters). |
| X2 | System.Double | X-coordinate of the line end point (meters). |
| Y2 | System.Double | Y-coordinate of the line end point (meters). |
| Z2 | System.Double | Z-coordinate of the line end point (meters). |

\*Remarks\*: Returns a \`SketchSegment\` object. Requires an active sketch.\[44\]

#### **2.2.2. Centerline Creation**

* **Method: ISketchManager::CreateCenterLine** Creates a construction line (centerline) between two points. *Source parameters based on.45*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| X1 | System.Double | X-coordinate of the first end point (meters). |
| Y1 | System.Double | Y-coordinate of the first end point (meters). |
| Z1 | System.Double | Z-coordinate of the first end point (meters). |
| X2 | System.Double | X-coordinate of the second end point (meters). |
| Y2 | System.Double | Y-coordinate of the second end point (meters). |
| Z2 | System.Double | Z-coordinate of the second end point (meters). |

\*Remarks\*: Returns a \`SketchSegment\` object. Alternatively, a centerline can be created using \`ISketchManager::CreateLine\` and then setting the \`ISketchSegment::ConstructionGeometry\` property to \`True\`.\[45, 46\]

#### **2.2.3. Circle Creation**

* **Method: ISketchManager::CreateCircle** Creates a circle defined by a center point and a point on its circumference. *Source parameters based on.42*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| XC | System.Double | X-coordinate of the circle center point (meters). |
| YC | System.Double | Y-coordinate of the circle center point (meters). |
| Zc | System.Double | Z-coordinate of the circle center point (meters). |
| Xp | System.Double | X-coordinate of a point on the circle (meters). |
| Yp | System.Double | Y-coordinate of a point on the circle (meters). |
| Zp | System.Double | Z-coordinate of a point on the circle (meters). |

\*Remarks\*: Returns a \`SketchSegment\` object. Requires an active sketch.\[42\]

* **Method: ISketchManager::CreateCircleByRadius** Creates a circle defined by a center point and a radius. *Source parameters based on.39*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| XC | System.Double | X-coordinate of the circle center point (meters). |
| YC | System.Double | Y-coordinate of the circle center point (meters). |
| Zc | System.Double | Z-coordinate of the circle center point (meters). |
| Radius | System.Double | Radius of the circle (meters). |

\*Remarks\*: Returns a \`SketchSegment\` object. Requires an active sketch.\[39\]

#### **2.2.4. Arc Creation**

* **Method: ISketchManager::CreateArc** Creates an arc defined by a center point, start point, end point, and direction. *Source parameters based on.38*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| XC | System.Double | X-coordinate of the arc's center point (meters). |
| YC | System.Double | Y-coordinate of the arc's center point (meters). |
| Zc | System.Double | Z-coordinate of the arc's center point (meters). |
| X1 | System.Double | X-coordinate of the arc's start point (meters). |
| Y1 | System.Double | Y-coordinate of the arc's start point (meters). |
| Z1 | System.Double | Z-coordinate of the arc's start point (meters). |
| X2 | System.Double | X-coordinate of the arc's end point (meters). |
| Y2 | System.Double | Y-coordinate of the arc's end point (meters). |
| Z2 | System.Double | Z-coordinate of the arc's end point (meters). |
| Direction | System.Short | Direction of arc: \+1 for counter-clockwise (CCW), \-1 for clockwise (CW). |

\*Remarks\*: Returns a \`SketchSegment\` object. Creates a partial arc in the active 2D sketch.\[38\]

* Method: ISketchManager::Create3PointArc 47  
  Creates an arc passing through three specified points. (Detailed parameters not available in snippets; typically takes three sets of X,Y,Z coordinates).  
* Method: ISketchManager::CreateTangentArc 47  
  Creates an arc tangent to a sketch entity at a specified point, with a given endpoint. (Detailed parameters not available in snippets; typically requires selection of a tangent entity and specification of the arc's endpoint).

#### **2.2.5. Rectangle Creation**

* **Method: ISketchManager::CreateCornerRectangle** Creates a rectangle defined by two opposite corner points. *Source parameters based on.83*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| X1 | System.Double | X-coordinate of the first corner point (e.g., upper-left) (meters). |
| Y1 | System.Double | Y-coordinate of the first corner point (meters). |
| Z1 | System.Double | Z-coordinate of the first corner point (meters). |
| X2 | System.Double | X-coordinate of the opposite corner point (e.g., lower-right) (meters). |
| Y2 | System.Double | Y-coordinate of the opposite corner point (meters). |
| Z2 | System.Double | Z-coordinate of the opposite corner point (meters). |

\*Remarks\*: Returns an \`System.Object\` which is an array of \`SketchSegment\` objects representing the four lines of the rectangle.

* **Method: ISketchManager::CreateCenterRectangle** Creates a rectangle defined by its center point and one corner point. *Source parameters based on.85*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| X1 | System.Double | X-coordinate of the rectangle's center point (meters). |
| Y1 | System.Double | Y-coordinate of the rectangle's center point (meters). |
| Z1 | System.Double | Z-coordinate of the rectangle's center point (meters). |
| X2 | System.Double | X-coordinate of one corner point of the rectangle (meters). |
| Y2 | System.Double | Y-coordinate of one corner point of the rectangle (meters). |
| Z2 | System.Double | Z-coordinate of one corner point of the rectangle (meters). |

\*Remarks\*: Returns an \`System.Object\` (array of \`SketchSegment\`).

* **Other Rectangle Creation Methods** 47:  
  * ISketchManager::Create3PointCenterRectangle: Creates a center rectangle at any angle.  
  * ISketchManager::Create3PointCornerRectangle: Creates a corner rectangle at any angle.  
  * ISketchManager::CreateParallelogram: Creates a parallelogram. *(Detailed parameters for these methods were not available in the provided snippets.)*

#### **2.2.6. Polygon Creation**

* **Method: ISketchManager::CreatePolygon** Creates a regular polygon defined by a center point, a vertex, the number of sides, and whether it's inscribed or circumscribed. *Source parameters based on.86*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| XC | System.Double | X-coordinate of the polygon's center (meters). |
| YC | System.Double | Y-coordinate of the polygon's center (meters). |
| Zc | System.Double | Z-coordinate of the polygon's center (meters). |
| Xp | System.Double | X-coordinate of one vertex of the polygon (meters). |
| Yp | System.Double | Y-coordinate of one vertex of the polygon (meters). |
| Zp | System.Double | Z-coordinate of one vertex of the polygon (meters). |
| Sides | System.Integer | Number of sides for the polygon (e.g., 3 for triangle, 6 for hexagon). |
| Inscribed | System.Boolean | True to show an inscribed construction circle, False for a circumscribed construction circle. |

\*Remarks\*: Returns an \`System.Object\` (array of \`SketchSegment\`).

#### **2.2.7. Ellipse Creation**

* **Method: ISketchManager::CreateEllipse** Creates a full ellipse defined by its center point, a point on its major axis, and a point on its minor axis. *Source parameters based on.41*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| XC | System.Double | X-coordinate of the ellipse center point (meters). |
| YC | System.Double | Y-coordinate of the ellipse center point (meters). |
| Zc | System.Double | Z-coordinate of the ellipse center point (meters). |
| XMajor | System.Double | X-coordinate of a point on the ellipse lying on its major axis (meters). |
| YMajor | System.Double | Y-coordinate of a point on the ellipse lying on its major axis (meters). |
| ZMajor | System.Double | Z-coordinate of a point on the ellipse lying on its major axis (meters). |
| XMinor | System.Double | X-coordinate of a point on the ellipse lying on its minor axis (meters). |
| YMinor | System.Double | Y-coordinate of a point on the ellipse lying on its minor axis (meters). |
| ZMinor | System.Double | Z-coordinate of a point on the ellipse lying on its minor axis (meters). |

\*Remarks\*: Returns a \`SketchSegment\` (specifically an \`ISketchEllipse\`). Requires an active 2D sketch.\[41\]

* **Method: ISketchManager::CreateEllipticalArc** 47 Creates a partial ellipse (elliptical arc). (Detailed parameters not available in snippets; typically involves center, major/minor axis points, and start/end angle or points for the arc segment).

#### **2.2.8. Spline Creation**

Splines are complex curves often defined by control points or equations.

* **Method: ISketchManager::CreateSpline3** This is the current method for creating 2D splines or splines constrained to a surface, superseding CreateSpline and CreateSpline2. *Source parameters based on.48*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| PointData | System.Object | Array of X,Y,Z coordinates of the spline points (meters). For 2D, at least 6 coordinates (2 points) are needed. Z values are often ignored for 2D sketches. |
| Surfs | System.Object | Array of ISurface objects. Null or Nothing for 2D splines. For on-surface splines, these are the surfaces for projection. |
| Direction | System.Object | Array of IMathVector objects. Valid only for on-surface splines, defines projection directions for each point in PointData. If Null, current view vector is used. |
| SimulateNaturalEnds | System.Boolean | For 2D splines only. True for zero curvature end conditions, False to maintain curvature at ends. |
| Status | System.Object (Out) | For on-surface splines only. Array of Booleans indicating projection success for each point. |

\*Remarks\*: Returns a \`System.Object\` (typically an \`ISketchSpline\`). Creates 2D spline in active sketch, or a new 3D sketch for on-surface splines (requiring \`ISketchManager::InsertSketch\` to finalize). Does not work with \`AddToDB\`/\`DisplayWhenAdded\`; always adds directly to database.\[48\]

* **Method: ISketchManager::CreateEquationSpline2** Creates an equation-driven 2D explicit/parametric curve or a 3D parametric curve, superseding CreateEquationSpline. *Source parameters based on.49*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| XExpression | System.String | Equation for x in terms of t (parametric), or empty string "" for explicit y=f(x). |
| YExpression | System.String | Equation for y in terms of t (parametric), or equation for y in terms of x (explicit). |
| ZExpression | System.String | Equation for z in terms of t (3D parametric). Empty for 2D. |
| RangeStart | System.String | Start value for parameter t (parametric) or x (explicit). Can be numeric or string like "0" or "-pi/2". |
| RangeEnd | System.String | End value for parameter t (parametric) or x (explicit). Can be numeric or string like "4\*pi". |
| IsAngleRange | System.Boolean | Legacy parameter, generally not needed. |
| RotationAngle | System.Double | Legacy parameter, generally not needed. |
| XOffset | System.Double | Legacy parameter, generally not needed. |
| YOffset | System.Double | Legacy parameter, generally not needed. |
| LockStart | System.Boolean | (From C\# syntax) True to lock the start point of the spline. |
| LockEnd | System.Boolean | (From C\# syntax) True to lock the end point of the spline. |

\*Remarks\*: Returns a \`SketchSpline\` object. Examples: Parabola y=x^2: \`CreateEquationSpline2("", "x^2", "", "-5", "5", False, 0, 0, 0, True, True)\`. Helix x=sin(t), y=cos(t), z=t/5: \`CreateEquationSpline2("sin(t)", "cos(t)", "t/5", "0", "30", False, 0, 0, 0, True, True)\`.\[49\]

* **Method: ISketchManager::ICreateSplineByEqnParams (and CreateSplineByEqnParams)** Creates a B-curve from B-spline data (control points and knot vector). *Source parameters based on.50*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Properties | System.Integer (ByRef) | In-process C++: Pointer to array of {Dimension, Order, NumControlPoints, Periodicity}. **Not supported for.NET (VB.NET, C\#, C++/CLI) or VBA.** |
| KnotArrayCount | System.Integer | Number of knots. |
| Knots | System.Double (ByRef) | In-process C++: Pointer to array of knots. **Not supported for.NET or VBA.** |
| ControlPointArrayCount | System.Integer | Number of control points. |
| ControlPoints | System.Double (ByRef) | In-process C++: Pointer to array of control points (x,y,\[z\],\[w\]). **Not supported for.NET or VBA.** |

\*Remarks\*: Returns a \`SketchSegment\`. The parameters \`Properties\`, \`Knots\`, and \`ControlPoints\` are problematic for managed code like Python due to their expectation of direct memory pointers.\[50\] This poses a significant challenge for creating Python wrappers for this specific method. Alternative approaches for creating splines from control points in a.NET-friendly way should be investigated if this functionality is critical.

* **Method: ISketchManager::CreateSplineParamData** 51 Creates and returns an empty SplineParamData object. This object is then populated and used with other spline creation or modification methods. This method itself takes no parameters.

#### **2.2.9. Point Creation**

* **Method: ISketchManager::CreatePoint** Creates a sketch point at specified coordinates. *Source parameters based on.43*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| X | System.Double | X-coordinate of the point (meters). |
| Y | System.Double | Y-coordinate of the point (meters). |
| Z | System.Double | Z-coordinate of the point (meters); ignored for 2D sketches. |

\*Remarks\*: Returns a \`SketchPoint\` object. Requires an active sketch.\[43\]

#### **2.2.10. Sketch Offset**

Offsets selected sketch entities by a specified distance.

* **Method: ISketchManager::SketchOffset2** This is the current method for offsetting sketch entities, superseding SketchOffset. *Source parameters based on.88*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Offset | System.Double | Offset distance (meters). A negative value offsets in the opposite direction. |
| BothDirections | System.Boolean | True to offset in both directions from the original entities. |
| Chain | System.Boolean | True to offset the entire chain of contiguous selected entities. False to offset only the explicitly selected entities. |
| CapEnds | System.Integer | Type of end cap for bidirectional offsets, as defined in swSkOffsetCapEndType\_e (e.g., swSkOffsetCapEnd\_None, swSkOffsetCapEnd\_Arcs, swSkOffsetCapEnd\_Lines). |
| MakeConstruction | System.Integer | Converts entities to construction geometry, as defined in swSkOffsetMakeConstructionType\_e (e.g., swSkOffsetMakeConstruction\_None, swSkOffsetMakeConstruction\_Original, swSkOffsetMakeConstruction\_Offset, swSkOffsetMakeConstruction\_Both). |
| AddDimensions | System.Boolean | True to add an offset dimension to the sketch. |

\*Remarks\*: Returns \`True\` on success. Requires pre-selection of sketch entities to offset.

* Obsolete Method: ISketchManager::SketchOffset 52  
  This method is superseded by SketchOffset2. Its parameters were similar but used booleans for CapEnds and MakeConstruction.  
* Related Method for 3D Offsets: IModelDocExtension::SketchOffsetOnSurface 53 and IModelDocExtension::GeodesicSketchOffset 54  
  These methods, part of IModelDocExtension, are used for creating offsets of 3D sketch entities on surfaces.

#### **2.2.11. Sketch Trim**

Trims sketch entities to their intersection with other entities.

* **Method: ISketchManager::SketchTrim** *Source parameters based on.55*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Option | System.Integer | Sketch trim option as defined in swSketchTrimChoice\_e. |
| X | System.Double | X-coordinate of pick location (meters). Used primarily for swSketchTrimEntityPoint. For other options, typically 0.0. |
| Y | System.Double | Y-coordinate of pick location (meters). Used primarily for swSketchTrimEntityPoint. For other options, typically 0.0. |
| Z | System.Double | Z-coordinate of pick location (meters, for 3D sketch). Used primarily for swSketchTrimEntityPoint. For other options, typically 0.0. |

\*Remarks\*: Returns \`True\` on success. Requires an active sketch and pre-selection of entities to trim using \`IModelDocExtension::SelectByID2\`.  
Key \`swSketchTrimChoice\_e\` options include \[55\]:  
\*   \`swSketchTrimClosest\`: Trims selected segment to nearest intersection. Select one segment.  
\*   \`swSketchTrimCorner\`: Extends/trims two selected segments to form a corner. Select two segments.  
\*   \`swSketchTrimEntities\` (Power trim): Trims multiple segments based on pick points. Select one or more segments with pick points.  
\*   \`swSketchTrimEntityPoint\`: Trims a single selected segment to a specific point (X,Y,Z) on that segment.  
\*   \`swSketchTrimInside\`: Trims segments inside a boundary formed by two other selected segments. Select boundary (2) and segments to trim (1+).  
\*   \`swSketchTrimOutside\`: Trims segments outside a boundary. Select boundary (2) and segments to trim (1+).  
\*   \`swSketchTrimTwoEntities\`: Trims the first selected segment to the second intersecting selected segment.

#### **2.2.12. Sketch Extend**

Extends a sketch entity to meet the nearest intersecting sketch entity.

* **Method: ISketchManager::SketchExtend** *Source parameters based on.56*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| X | System.Double | X-coordinate of a pick point on the sketch entity to extend (meters). Documentation suggests using 0.0.56 |
| Y | System.Double | Y-coordinate of a pick point on the sketch entity to extend (meters). Documentation suggests using 0.0.56 |
| Z | System.Double | Z-coordinate of a pick point on the sketch entity to extend (meters, for 3D sketch). Documentation suggests using 0.0.56 |

\*Remarks\*: Returns \`True\` on success. The coordinates are used to identify the entity to extend. Requires an active sketch and pre-selection of the entity.

#### **2.2.13. Convert Entities (Projecting Edges/Curves to Sketch Plane)**

Creates sketch entities by projecting existing model edges, loops, faces, curves, or external sketch contours onto the active sketch plane.

* **Method: ISketchManager::SketchUseEdge3** This is the current API method corresponding to the "Convert Entities" command. The method ISketchManager::ConvertEntities is listed as "Not implemented. Use ISketchManager::SketchUseEdge2." 47, and SketchUseEdge3 appears to be the latest version. *Source parameters based on.89*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Chain | System.Boolean | True to convert the entire chain of contiguous selected entities (e.g., all edges of a loop). False to convert only the explicitly selected entities. |
| InnerLoops | System.Boolean | True to convert the inner loops of selected faces to sketch entities, False to not. |

\*Remarks\*: Returns \`True\` on success. Requires pre-selection of the model geometry (edges, faces, curves, etc.) to be converted. Creates "On Edge" relations between the new sketch entities and the source geometry, so the sketch entities update if the source geometry changes.\[57\]

#### **2.2.14. Sketch Fillet**

Creates a rounded corner (fillet) between two intersecting sketch entities.

* **Method: ISketchManager::CreateFillet** 47 (Detailed parameters were not available in the provided snippets. Typically, this method would require a radius value and pre-selection of two sketch entities or a sketch vertex where entities meet.)

#### **2.2.15. Sketch Chamfer**

Creates a beveled corner (chamfer) between two intersecting sketch entities.

* **Method: ISketchManager::CreateChamfer** *Source parameters based on 73 (TheCADCoder blog, which references swSketchChamferType\_e).*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Type | System.Integer | Type of chamfer as defined in swSketchChamferType\_e (e.g., swSketchChamfer\_DistanceEqual, swSketchChamfer\_DistanceAngle, swSketchChamfer\_DistanceDistance). |
| Distance1 | System.Double | First chamfer distance (meters). For swSketchChamfer\_DistanceEqual, this is the equal distance. For swSketchChamfer\_DistanceAngle, this is the distance. |
| AngleOrDistance2 | System.Double | Second chamfer distance (meters) if Type is swSketchChamfer\_DistanceDistance. Chamfer angle (radians) if Type is swSketchChamfer\_DistanceAngle. Ignored if Type is swSketchChamfer\_DistanceEqual. |

\*Remarks\*: Requires pre-selection of two intersecting sketch entities or a sketch vertex.

#### **2.2.16. Linear Sketch Pattern**

Creates a linear pattern of selected sketch entities.

* **Method: ISketchManager::CreateLinearSketchStepAndRepeat** *Source parameters based on.91*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| NumX | System.Integer | Total number of instances along Direction 1 (X-axis relative to pattern angle), including the seed. |
| NumY | System.Integer | Total number of instances along Direction 2 (Y-axis relative to pattern angle), including the seed. (Set to 1 if no Y-direction pattern). |
| SpacingX | System.Double | Spacing between instances along Direction 1 (meters). |
| SpacingY | System.Double | Spacing between instances along Direction 2 (meters). |
| AngleX | System.Double | Angle for Direction 1 relative to the sketch's X-axis (radians). |
| AngleY | System.Double | Angle for Direction 2 relative to the sketch's Y-axis (radians). (Typically AngleX \+ π/2 for orthogonal pattern). |
| DeleteInstances | System.String | String specifying indices of instances to skip/delete, e.g., "(1) (3)". Indices are 1-based. |
| XSpacingDim | System.Boolean | True to display the spacing dimension for Direction 1\. |
| YSpacingDim | System.Boolean | True to display the spacing dimension for Direction 2\. |
| AngleDim | System.Boolean | True to display the angle dimension between pattern axes. |
| CreateNumOfInstancesDimInXDir | System.Boolean | True to display the instance count dimension for Direction 1\. |
| CreateNumOfInstancesDimInYDir | System.Boolean | True to display the instance count dimension for Direction 2\. |

\*Remarks\*: Requires pre-selection of sketch entities to pattern.

* **Method: ISketchManager::EditLinearSketchStepAndRepeat** 47 Edits an existing linear sketch pattern. (Parameters are expected to be similar to creation, plus an identifier for the pattern feature).

#### **2.2.17. Circular Sketch Pattern**

Creates a circular pattern of selected sketch entities around a center point.

* **Method: ISketchManager::CreateCircularSketchStepAndRepeat** *Source parameters based on.9358*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| ArcRadius | System.Double | Radius of the circular sketch pattern (distance from pattern center to seed entities' reference point) (meters). |
| ArcAngle | System.Double | Total angle for the pattern if equally spaced, or angle to the first instance (radians). Context-dependent. |
| PatternNum | System.Integer | Total number of instances, including the seed. |
| PatternSpacing | System.Double | Angular spacing between instances (radians), if not using total angle for equal spacing. |
| PatternRotate | System.Boolean | True to rotate patterned instances relative to the center. False to maintain original orientation. |
| DeleteInstances | System.String | String specifying indices of instances to skip/delete, e.g., "(1) (3)". |
| RadiusDim | System.Boolean | True to display the radius dimension of the pattern. |
| AngleDim | System.Boolean | True to display the angular spacing or total angle dimension. |
| CreateNumOfInstancesDim | System.Boolean | True to display the instance count dimension. |
| Seeds | System.String | Underscore-separated string of names of the sketch entities comprising the seed pattern (e.g., "Line1\_Arc1"). |

\*Remarks\*: Requires pre-selection of sketch entities to pattern and definition of the pattern center point (often by selecting a sketch point before calling or by implicit definition).

* **Method: ISketchManager::EditCircularSketchStepAndRepeat** 47 Edits an existing circular sketch pattern. Parameters are detailed in 58 and 58, mirroring the creation parameters plus an identifier for the pattern.

#### **2.2.18. Sketch Mirror**

Mirrors selected sketch entities about a selected centerline.

* No direct method named MirrorEntities or CreateSketchMirror is consistently listed for the SolidWorks ISketchManager interface in the provided documentation snippets.47  
* **Dynamic Mirroring**: The ISketchManager::SetDynamicMirror(True) method enables a mode where newly created sketch entities are automatically mirrored about a pre-selected centerline.47 This is for real-time mirroring during creation, not for mirroring existing entities.  
* The DraftSight API (a different CAD product, though its API structure can sometimes be similar) does list MirrorEntities under its ISketchManager.59 This contrast suggests that a direct "mirror existing entities" command might be implemented differently or be absent in the SolidWorks ISketchManager.  
* To mirror existing sketch entities in SolidWorks via API, one might need to:  
  1. Pre-select the entities to mirror and the mirror line.  
  2. Potentially use a more general transformation command if available (not detailed in snippets).  
  3. Alternatively, copy the entities (ISketchManager::Copy), then apply a geometric transformation (e.g., using ISketchManager::RotateOrCopy3DAboutXYZ or similar if a 2D equivalent exists, or by directly manipulating entity coordinates if their API objects allow it) relative to the mirror line. This would be a more complex, manual approach.

### **2.3. Sketch Relations APIs (ISketchRelationManager)**

Sketch relations (constraints) define the geometric relationships between sketch entities, driving the parametric behavior of sketches. The ISketchRelationManager interface, typically accessed from an active ISketch object, is used to manage these relations.

* **Method: ISketchRelationManager::AddRelation** Adds a geometric relation between specified sketch entities. *Source parameters based on.94*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| Entities | System.Object | An array of ISketchEntity objects (or dispatch pointers to them) that are involved in the relation. The number and type of entities depend on the RelationType. |
| RelationType | System.Integer | The type of geometric relation to apply, as defined in the swConstraintType\_e enumeration. |

\*Remarks\*: This method requires an active sketch. The \`swConstraintType\_e\` enumeration includes values for common relations such as \`swConstraintType\_COINCIDENT\`, \`swConstraintType\_CONCENTRIC\`, \`swConstraintType\_PARALLEL\`, \`swConstraintType\_PERPENDICULAR\`, \`swConstraintType\_HORIZONTAL\`, \`swConstraintType\_VERTICAL\`, \`swConstraintType\_TANGENT\`, \`swConstraintType\_EQUAL\`, \`swConstraintType\_MIDPOINT\`, \`swConstraintType\_INTERSECTION\`, \`swConstraintType\_SYMMETRIC\`, \`swConstraintType\_CORADIAL\`, etc. The \`Entities\` array must contain the appropriate number and types of sketch entities for the specified \`RelationType\`. For example, a \`swConstraintType\_COINCIDENT\` relation might take two points, or a point and a line/arc. A \`swConstraintType\_TANGENT\` relation typically involves an arc/circle/spline and a line/arc.

### **2.4. Dimensioning APIs (IModelDocExtension, IDisplayDimension)**

Dimensions control the size and position of sketch entities and features. They are primarily created using methods on the IModelDocExtension interface, and the created dimensions can be further customized using the IDisplayDimension interface.

* **Method: IModelDocExtension::AddDimension** This method behaves similarly to the "Smart Dimension" tool in the SolidWorks UI, creating a dimension based on the selected entities and the placement point. *Source parameters based on.61*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| X | System.Double | X-coordinate for the placement of the dimension text (meters). |
| Y | System.Double | Y-coordinate for the placement of the dimension text (meters). |
| Z | System.Double | Z-coordinate for the placement of the dimension text (meters). |
| Direction | System.Integer | For parts, specifies the dimensioning manipulator direction (swSmartDimensionDirection\_e) if an extension line is needed. For drawings, specifies the rapid dimensioning selector quadrant. |

\*Remarks\*: Returns a \`System.Object\` (which is an \`IDisplayDimension\`). Requires pre-selection of entities to be dimensioned by location (not name). For example, to create an angular dimension, select a sketch segment of the angle and the vertex of the angle before calling.\[61\] If entities unambiguously define the dimension, \`IModelDoc2::AddDimension2\` might be an alternative.

* **Method: IModelDocExtension::AddSpecificDimension** Creates a specific type of dimension (e.g., horizontal, vertical, diameter) for selected entities. *Source parameters based on.62*

| Parameter | Data Type | Description |
| :---- | :---- | :---- |
| X | System.Double | X-coordinate for the placement of the dimension text (meters). |
| Y | System.Double | Y-coordinate for the placement of the dimension text (meters). |
| Z | System.Double | Z-coordinate for the placement of the dimension text (meters). |
| DimensionType | System.Integer | Type of dimension to add, as defined in swDimensionType\_e (e.g., swAngularDimension, swDiameterDimension, swHorLinearDimension). |
| Error | System.Integer (Out) | Result status of the operation, as defined in swAddSpecificDimension\_e. |

\*Remarks\*: Returns a \`System.Object\` (an \`IDisplayDimension\`). Requires pre-selection of entities appropriate for the specified \`DimensionType\`.\[62\]

* **Interface: IDisplayDimension** The IDisplayDimension object, returned by dimension creation methods, allows for further querying and modification of the dimension's properties and appearance. *Key Properties of IDisplayDimension*: *Source properties based on.63*

| Property | Data Type | Description |
| :---- | :---- | :---- |
| Type2 | System.Integer | Gets the type of dimension (e.g., swDimensionType\_e). |
| ArrowSide | System.Integer | Gets or sets the position of the dimension arrows (e.g., swDimensionArrowSide\_e). |
| CenterText | System.Boolean | Gets or sets whether the dimension text should be automatically centered. |
| Diametric | System.Boolean | Gets or sets whether a radial/linear dimension is displayed as diametric (doubled distance) or radial (single distance). |
| DimensionToInside | System.Boolean | Gets or sets whether dimensions to arcs are always to the inside of the arc. |
| DisplayAsChain | System.Boolean | Gets or sets whether extension lines of angular running or ordinate dimensions are chained. |
| DisplayAsLinear | System.Boolean | Gets or sets whether a diameter dimension is displayed as a linear dimension. |
| HorizontalJustification | System.Integer | Gets or sets the dimension text's horizontal justification (e.g., swDimensionTextHorizontalJustification\_e). |
| VerticalJustification | System.Integer | Gets or sets the dimension text's vertical justification (e.g., swDimensionTextVerticalJustification\_e). |
| Inspection | System.Boolean | Gets or sets whether a display dimension above the dimension line is an inspection dimension. |
| IsLinked | System.Boolean | Gets whether the dimension text is linked to a property or variable. |
| MarkedForDrawing | System.Boolean | Gets or sets whether this model dimension is marked to be included in a drawing. |
| SolidLeader | System.Boolean | Gets or sets whether this display dimension is displayed with a solid leader for arc and radial dimensions.63 |
| WitnessVisibility | System.Integer | Gets or sets which extension lines (witness lines) are visible (e.g., swDimensionWitnessLineVisibility\_e). |

Manipulating these properties is essential for controlling the final appearance and behavior of dimensions created through automation.

## **3\. Guidance for Python Wrapper Development**

Developing Python wrappers for the SolidWorks.NET API requires careful consideration of COM interoperability, data type handling, entity selection, error management, and application lifecycle control.

### **3.1. Interfacing with SolidWorks COM APIs from Python**

The SolidWorks API is a COM interface, which Python can interact with primarily through libraries like pythonnet or pywin32. pythonnet is often preferred for.NET APIs as it allows more direct interaction with.NET assemblies and types.  
The first step in an automation script is typically to obtain an instance of the SolidWorks application object, SldWorks.SldWorks. This can be achieved by connecting to a running instance of SolidWorks or by launching a new one.6  
Once the application object is obtained, key manager objects can be accessed from the active document (IModelDoc2 object):

* IModelDoc2.FeatureManager provides the IFeatureManager interface.  
* IModelDoc2.SketchManager provides the ISketchManager interface.  
* IModelDoc2.Extension provides the IModelDocExtension interface.  
* ISketchManager.ActiveSketch.RelationManager (or similar path) provides the ISketchRelationManager for an active sketch.

### **3.2. Handling SolidWorks Data Types and Enums**

SolidWorks API methods use.NET data types such as System.Double, System.Boolean, System.Integer, System.String, and System.Object. When using pythonnet, these often map intuitively to Python's float, bool, int, and str.  
SolidWorks enumerations (e.g., swEndConditions\_e, swSelectType\_e, swFeatureNameID\_e) are critical for specifying options and types. With pythonnet, these enums are typically accessible as static members of their respective interop types (e.g., SolidWorks.Interop.swconst.swEndConditions\_e.swEndCondBlind).7  
A common challenge arises with parameters of type System.Object that expect arrays of values (e.g., coordinates for a spline, entities for a relation). Python lists or tuples must be correctly marshaled into.NET arrays of the appropriate underlying type (e.g., an array of System.Double for coordinates, or an array of dispatch pointers/System.Object for entities). Libraries like pythonnet provide mechanisms for creating and passing these.NET arrays from Python. For example, IFeatureFillet3::Radii 13 and ISketchManager::CreateSpline3::PointData 66 are such cases.  
Some API methods, particularly older or C++-style ones like ISketchManager::ICreateSplineByEqnParams, expect direct memory pointers for array parameters, which are explicitly marked as "Not supported" for.NET languages and VBA.50 This presents a significant hurdle for direct Python wrapping, and alternative APIs or workarounds may be necessary for such functionalities.

### **3.3. Pre-selection and Entity Handling**

Many SolidWorks API methods operate on pre-selected entities. The IModelDocExtension.SelectByID2 method is fundamental for this purpose.6 Its key parameters include:

* Name (String): Name of the object (can be empty if selecting by coordinates).  
* Type (String): Type of object (e.g., "SKETCH", "PLANE", "SOLIDBODY", "EDGE", "FACE") from swSelectType\_e.  
* X, Y, Z (Double): Coordinates for selection, particularly for sub-elements like edges or vertices.  
* Append (Boolean): True to add to the current selection, False to clear existing selections first.  
* Mark (Integer): A user-defined integer value used by subsequent API calls to identify the role of the selected entity. The meaning of the Mark is context-dependent (e.g., Mark 1 for profile, Mark 4 for path in sweep creation 9; Mark 16 for axis of revolution 8). This is a critical parameter to set correctly.  
* Callout (Object): Pointer to an associated callout (often Nothing or null).  
* SelectOption (Integer): Selection options from swSelectOption\_e.

Objects returned by API methods are often COM dispatch pointers. pythonnet typically handles the marshalling of these objects into usable Python proxy objects.

### **3.4. Error Handling Strategies**

Robust automation scripts must include comprehensive error handling.

* **Return Values**: Many API methods return a System.Boolean indicating success/failure, or a System.Object which might be null (translating to None in Python) if the operation failed.6 These should always be checked.  
* **Specific Error Parameters**: Some methods include an explicit output parameter for error codes, such as Error As System.Integer in IModelDocExtension::AddSpecificDimension 62, which provides an swAddSpecificDimension\_e enum value.  
* **Feature Creation Errors**: For errors during feature creation, IFeatureManager::GetCreateFeatureErrors can be called to retrieve detailed error messages.67 Additionally, after a feature is created (or attempted), IFeature::GetErrorCode2 can provide error codes specific to that feature's generation.4  
* **COM Exceptions**: Standard Python try-except blocks should be used to catch COM exceptions that might be raised by the API, often indicating invalid parameters, incorrect state, or internal SolidWorks errors.

### **3.5. Managing SolidWorks Application and Document Lifecycle**

Wrappers must manage the SolidWorks application and document states:

* **Application**: Connect to or launch SolidWorks (SldWorks.SldWorks).  
* **Documents**:  
  * Open existing documents: ISldWorks::OpenDoc7 (allows specifying configuration, display state, etc.).  
  * Create new documents: ISldWorks::NewDocument 64 (requires a template path).  
  * Activate documents: ISldWorks::ActivateDoc2 (if multiple documents are open).  
  * Save documents: IModelDoc2::Save3, IModelDoc2::SaveAs.  
  * Close documents: ISldWorks::CloseDoc.  
* **Sketch Mode**:  
  * Enter sketch mode: ISketchManager::InsertSketch(True) on a selected plane or face.  
  * Exit sketch mode: ISketchManager::InsertSketch(True) again (or False to exit without saving changes to the sketch).  
  * Check for active sketch: ISketchManager::ActiveSketch returns the active ISketch object or null.6

### **3.6. Performance Considerations for Automation**

For scripts performing many operations, performance is key:

* **Sketch Creation**: As mentioned, use ISketchManager::AddToDB(True) and ISketchManager::DisplayWhenAdded(False) before creating a batch of sketch entities, and revert these settings afterwards. This bypasses UI-related overhead like inferencing and immediate display updates.38  
* **Feature Tree Updates**: Temporarily disable FeatureManager design tree updates using IFeatureManager::EnableFeatureTree(False) before a series of feature creations or modifications, and re-enable it with IFeatureManager::EnableFeatureTree(True) afterwards. This can significantly speed up operations by preventing the UI from redrawing after each step.69  
* **Automatic Rebuilds**: Suspend automatic model rebuilds using IModelDoc2::SetSuspendRebuild(True) (or a similar method) before making multiple geometric changes, and then call IModelDoc2::ForceRebuild3(False) (or SetSuspendRebuild(False) followed by a manual rebuild) to update the model once all changes are made.70

## **4\. Conclusion**

### **4.1. Recap of the API Registry's Utility**

This document has compiled a detailed registry of SolidWorks.NET API methods relevant to common feature and sketch creation tasks. By providing specific method signatures, parameter descriptions, data types, and references to pertinent enumerations, this registry serves as a foundational resource for developers aiming to create Python wrappers for SolidWorks automation. The emphasis on modern API patterns, such as the CreateDefinition/CreateFeature paradigm for feature generation and best practices for sketch entity creation, is intended to promote the development of robust, maintainable, and efficient automation solutions. The structured presentation of API parameters in tabular format directly addresses the need for clear, accessible information crucial for wrapper implementation.

### **4.2. Recommendations for Further Exploration**

While this registry covers a significant range of common functionalities, the SolidWorks API is extensive. Developers are encouraged to:

* **Consult the Official SolidWorks API Help**: If accessible, the complete API documentation (typically available via the Help menu in SolidWorks or online) remains the definitive source for all API calls, including those not covered here, more extensive code examples, and details on specific enumerations and object hierarchies.1  
* **Explore the SolidWorks.Interop.swconst Namespace**: This namespace contains a vast number of enumerations (e.g., swEndConditions\_e, swSelectType\_e, swFeatureNameID\_e) that are essential for providing correct parameter values to API methods.2 Familiarity with these constants is crucial.  
* **Utilize the SolidWorks Macro Recorder**: The macro recording feature in SolidWorks is an invaluable tool for discovering the API calls corresponding to specific UI operations.3 Recorded macros (typically in VBA) can then be analyzed, and the identified API calls can be looked up in the official documentation for detailed understanding and translation into Python.  
* **Leverage Community Resources**: Online forums, technical blogs (e.g., The CAD Coder 64, CodeStack.net 74, CADSharp 74), and the official SolidWorks API support webpage 3 can provide additional examples, solutions to common problems, and insights from experienced API developers.  
* **Pay Attention to API Versioning and Obsolescence**: SolidWorks regularly updates its API. It is important to use the API version corresponding to the target SolidWorks release and to heed warnings about obsolete methods, migrating to newer, supported APIs whenever possible to ensure long-term script compatibility and functionality.1

By combining the information in this registry with these further exploration strategies, developers can effectively harness the SolidWorks.NET API to build powerful automation tools using Python.

#### **Works cited**

1. Welcome \- 2025 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/sldworksapiprogguide/Welcome.htm](https://help.solidworks.com/2025/english/api/sldworksapiprogguide/Welcome.htm)  
2. SOLIDWORKS API Help \- 2025 \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/SWHelp\_List.html?id=d2568ea68039499cbecb5d2ea919e8f0\#Pg0](https://help.solidworks.com/2025/english/api/SWHelp_List.html?id=d2568ea68039499cbecb5d2ea919e8f0#Pg0)  
3. SOLIDWORKS API \- 2025, accessed May 16, 2025, [https://help.solidworks.com/2025/english/SWConnected/swdotworks/c\_solidworks\_api.htm](https://help.solidworks.com/2025/english/SWConnected/swdotworks/c_solidworks_api.htm)  
4. CreateFeature Method (IFeatureManager) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IFeatureManager\~CreateFeature.html](https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IFeatureManager~CreateFeature.html)  
5. How to work with Features in the SOLIDWORKS API (part 3\) \- CAD Booster, accessed May 16, 2025, [https://cadbooster.com/how-to-work-with-features-in-the-solidworks-api/](https://cadbooster.com/how-to-work-with-features-in-the-solidworks-api/)  
6. Create Extrusion Feature Example (VBA) \- 2021 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2021/English/api/sldworksapi/Insert\_Feature\_Extrusion\_Example\_VB.htm](https://help.solidworks.com/2021/English/api/sldworksapi/Insert_Feature_Extrusion_Example_VB.htm)  
7. FeatureExtrusion3 Method (IFeatureManager) \- 2023 ..., accessed May 16, 2025, [https://help.solidworks.com/2023/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IFeatureManager\~FeatureExtrusion3.html](https://help.solidworks.com/2023/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IFeatureManager~FeatureExtrusion3.html)  
8. FeatureRevolve2 Method (IFeatureManager) \- 2011 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2011/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IFeatureManager\~FeatureRevolve2.html](https://help.solidworks.com/2011/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IFeatureManager~FeatureRevolve2.html)  
9. Sweep Features and SweepFeatureData Objects \- 2024 ..., accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapiprogguide/Overview/Sweep\_Features\_and\_SweepFeatureData\_Objects.htm?id=ff4a38b911594306a0f3ecfe5a506834](https://help.solidworks.com/2024/english/api/sldworksapiprogguide/Overview/Sweep_Features_and_SweepFeatureData_Objects.htm?id=ff4a38b911594306a0f3ecfe5a506834)  
10. InsertProtrusionBlend2 Method (IFeatureManager) \- 2024 ..., accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IFeatureManager\~InsertProtrusionBlend2.html](https://help.solidworks.com/2024/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IFeatureManager~InsertProtrusionBlend2.html)  
11. FeatureExtrusionThin2 Method (IFeatureManager) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.ifeaturemanager\~featureextrusionthin2.html](https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.ifeaturemanager~featureextrusionthin2.html)  
12. IEdgeFlangeFeatureData Interface \- 2025 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IEdgeFlangeFeatureData.html?id=ba406d4841854c24af589a6fc64ec06d](https://help.solidworks.com/2025/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IEdgeFlangeFeatureData.html?id=ba406d4841854c24af589a6fc64ec06d)  
13. FeatureFillet3 Method (IFeatureManager) \- 2024 \- SOLIDWORKS ..., accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IFeatureManager\~FeatureFillet3.html](https://help.solidworks.com/2024/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IFeatureManager~FeatureFillet3.html)  
14. 2024 api \- ConstantWidth Property (ISimpleFilletFeatureData2) \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISimpleFilletFeatureData2\~ConstantWidth.html?format=P\&value=](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISimpleFilletFeatureData2~ConstantWidth.html?format=P&value)  
15. ConstantWidth Property (ISimpleFilletFeatureData2) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISimpleFilletFeatureData2\~ConstantWidth.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISimpleFilletFeatureData2~ConstantWidth.html)  
16. SetConicRhoOrRadius Method (ISimpleFilletFeatureData2) \- 2024 \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISimpleFilletFeatureData2\~SetConicRhoOrRadius.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISimpleFilletFeatureData2~SetConicRhoOrRadius.html)  
17. TwistControlType Property (ISweepFeatureData) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISweepFeatureData\~TwistControlType.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISweepFeatureData~TwistControlType.html)  
18. AlignWithEndFaces Property (ISweepFeatureData) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISweepFeatureData\~AlignWithEndFaces.html](https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISweepFeatureData~AlignWithEndFaces.html)  
19. AutoSelectComponents Property (ISweepFeatureData) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.ISweepFeatureData\~AutoSelectComponents.html](https://help.solidworks.com/2024/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ISweepFeatureData~AutoSelectComponents.html)  
20. CreateLoftBody Method (IModeler) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IModeler\~CreateLoftBody.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModeler~CreateLoftBody.html)  
21. InsertFeatureChamfer Method (IFeatureManager) \- 2020 ..., accessed May 16, 2025, [https://help.solidworks.com/2020/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IFeatureManager\~InsertFeatureChamfer.html](https://help.solidworks.com/2020/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IFeatureManager~InsertFeatureChamfer.html)  
22. 2020 api \- IDraftFeatureData Interface \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2020/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IDraftFeatureData.html?format=P\&value=](https://help.solidworks.com/2020/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IDraftFeatureData.html?format=P&value)  
23. FacesToDraft Property (IDraftFeatureData2) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IDraftFeatureData2\~FacesToDraft.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IDraftFeatureData2~FacesToDraft.html)  
24. IRibFeatureData2 Interface Properties \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.iribfeaturedata2\_properties.html](https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.iribfeaturedata2_properties.html)  
25. HoleWizard5 Method (IFeatureManager) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IFeatureManager\~HoleWizard5.html](https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IFeatureManager~HoleWizard5.html)  
26. Release Notes \- 2025 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/sldworksapi/ReleaseNotes-sldworksapi.html](https://help.solidworks.com/2025/english/api/sldworksapi/ReleaseNotes-sldworksapi.html)  
27. FeatureLinearPattern4 Method (IFeatureManager) \- 2024 ..., accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IFeatureManager\~FeatureLinearPattern4.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IFeatureManager~FeatureLinearPattern4.html)  
28. 2024 api \- D2EndRefOffset Property (ILinearPatternFeatureData) \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ILinearPatternFeatureData\~D2EndRefOffset.html?format=P\&value=](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ILinearPatternFeatureData~D2EndRefOffset.html?format=P&value)  
29. PatternFeatureArray Property (ILinearPatternFeatureData) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ILinearPatternFeatureData\~PatternFeatureArray.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ILinearPatternFeatureData~PatternFeatureArray.html)  
30. FeatureCircularPattern5 Method (IFeatureManager) \- 2024 ..., accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IFeatureManager\~FeatureCircularPattern5.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IFeatureManager~FeatureCircularPattern5.html)  
31. EqualSpacing Property (ICircularPatternFeatureData) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ICircularPatternFeatureData\~EqualSpacing.html](https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ICircularPatternFeatureData~EqualSpacing.html)  
32. PatternBodyArray Property (ICircularPatternFeatureData) \- 2025 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2025/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.ICircularPatternFeatureData\~PatternBodyArray.html](https://help.solidworks.com/2025/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ICircularPatternFeatureData~PatternBodyArray.html)  
33. 2024 api \- ISetPatternFaceArray Method (ICircularPatternFeatureData), accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ICircularPatternFeatureData\~ISetPatternFaceArray.html?format=P\&value=](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ICircularPatternFeatureData~ISetPatternFaceArray.html?format=P&value)  
34. SkippedItemArray Property (ICircularPatternFeatureData) \- 2022 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2022/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ICircularPatternFeatureData\~SkippedItemArray.html](https://help.solidworks.com/2022/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ICircularPatternFeatureData~SkippedItemArray.html)  
35. ReleaseSelectionAccess Method (ICircularPatternFeatureData) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ICircularPatternFeatureData\~ReleaseSelectionAccess.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ICircularPatternFeatureData~ReleaseSelectionAccess.html)  
36. InsertMirrorFeature2 Method (IFeatureManager) \- 2023 ..., accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IFeatureManager\~InsertMirrorFeature2.html](https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IFeatureManager~InsertMirrorFeature2.html)  
37. Merge Property (IMirrorSolidFeatureData) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IMirrorSolidFeatureData\~Merge.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IMirrorSolidFeatureData~Merge.html)  
38. CreateArc Method (ISketchManager) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~CreateArc.html](https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~CreateArc.html)  
39. CreateCircleByRadius Method (ISketchManager) \- 2025 ..., accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.isketchmanager\~createcirclebyradius.html](https://help.solidworks.com/2025/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.isketchmanager~createcirclebyradius.html)  
40. CreateLine2 Method (IModelDoc2) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IModelDoc2\~CreateLine2.html](https://help.solidworks.com/2023/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IModelDoc2~CreateLine2.html)  
41. CreateEllipse Method (ISketchManager) \- 2021 \- SOLIDWORKS API ..., accessed May 16, 2025, [https://help.solidworks.com/2021/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.ISketchManager\~CreateEllipse.html](https://help.solidworks.com/2021/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ISketchManager~CreateEllipse.html)  
42. CreateCircle Method (ISketchManager) \- 2020 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2020/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.isketchmanager\~createcircle.html](https://help.solidworks.com/2020/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.isketchmanager~createcircle.html)  
43. CreatePoint Method (ISketchManager) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.ISketchManager\~CreatePoint.html](https://help.solidworks.com/2024/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ISketchManager~CreatePoint.html)  
44. CreateLine Method (ISketchManager) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~CreateLine.html](https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~CreateLine.html)  
45. CreateCenterLine Method (ISketchManager) \- 2022 \- SOLIDWORKS ..., accessed May 16, 2025, [https://help.solidworks.com/2022/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.ISketchManager\~CreateCenterLine.html](https://help.solidworks.com/2022/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ISketchManager~CreateCenterLine.html)  
46. CreateCenterLine Method (ISketchManager) \- 2010 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2010/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~CreateCenterLine.html](https://help.solidworks.com/2010/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~CreateCenterLine.html)  
47. ISketchManager Interface Methods \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.isketchmanager\_methods.html](https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.isketchmanager_methods.html)  
48. CreateSpline3 Method (ISketchManager) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~CreateSpline3.html](https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~CreateSpline3.html)  
49. CreateEquationSpline2 Method (ISketchManager) \- 2025 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.isketchmanager\~createequationspline2.html](https://help.solidworks.com/2025/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.isketchmanager~createequationspline2.html)  
50. ICreateSplineByEqnParams Method (ISketchManager) \- 2025 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~ICreateSplineByEqnParams.html](https://help.solidworks.com/2025/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~ICreateSplineByEqnParams.html)  
51. CreateSplineParamData Method (ISketchManager) \- 2025 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.ISketchManager\~CreateSplineParamData.html](https://help.solidworks.com/2025/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ISketchManager~CreateSplineParamData.html)  
52. 2024 api \- SketchOffset Method (ISketchManager), accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.ISketchManager\~SketchOffset.html?format=P\&value=](https://help.solidworks.com/2024/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ISketchManager~SketchOffset.html?format=P&value)  
53. SketchOffsetOnSurface Method (IModelDocExtension) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IModelDocExtension\~SketchOffsetOnSurface.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDocExtension~SketchOffsetOnSurface.html)  
54. GeodesicSketchOffset Method (IModelDocExtension) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IModelDocExtension\~GeodesicSketchOffset.html](https://help.solidworks.com/2023/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDocExtension~GeodesicSketchOffset.html)  
55. SketchTrim Method (ISketchManager) \- 2021 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2021/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.isketchmanager\~sketchtrim.html](https://help.solidworks.com/2021/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.isketchmanager~sketchtrim.html)  
56. SketchExtend Method (ISketchManager) \- 2021 \- SOLIDWORKS API ..., accessed May 16, 2025, [https://help.solidworks.com/2021/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~SketchExtend.html](https://help.solidworks.com/2021/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~SketchExtend.html)  
57. Using Convert Entities \- 2024 \- SOLIDWORKS Help, accessed May 16, 2025, [https://help.solidworks.com/2024/English/SolidWorks/sldworks/t\_Using\_Convert\_Entities.htm](https://help.solidworks.com/2024/English/SolidWorks/sldworks/t_Using_Convert_Entities.htm)  
58. EditCircularSketchStepAndRepeat Method (ISketchManager) \- 2021 \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2021/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~EditCircularSketchStepAndRepeat.html](https://help.solidworks.com/2021/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~EditCircularSketchStepAndRepeat.html)  
59. ISketchManager Interface Members \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/draftsightapi/Interop.dsAutomation\~Interop.dsAutomation.ISketchManager\_members.html](https://help.solidworks.com/2024/English/api/draftsightapi/Interop.dsAutomation~Interop.dsAutomation.ISketchManager_members.html)  
60. ISketchManager Interface Methods \- 2022 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2022/english/api/draftsightapi/interop.dsautomation\~interop.dsautomation.isketchmanager\_methods.html](https://help.solidworks.com/2022/english/api/draftsightapi/interop.dsautomation~interop.dsautomation.isketchmanager_methods.html)  
61. AddDimension Method (IModelDocExtension) \- 2022 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2022/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IModelDocExtension\~AddDimension.html](https://help.solidworks.com/2022/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDocExtension~AddDimension.html)  
62. AddSpecificDimension Method (IModelDocExtension) \- 2024 ..., accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IModelDocExtension\~AddSpecificDimension.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDocExtension~AddSpecificDimension.html)  
63. 2025 api \- SolidLeader Property (IDisplayDimension) \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2025/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IDisplayDimension\~SolidLeader.html?format=P\&value=](https://help.solidworks.com/2025/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IDisplayDimension~SolidLeader.html?format=P&value)  
64. Create Line \- SOLIDWORKS C\# API \- The CAD Coder, accessed May 16, 2025, [https://thecadcoder.com/solidworks-csharp/create-line/](https://thecadcoder.com/solidworks-csharp/create-line/)  
65. SolidWorks.Interop.swconst Namespace \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/swconst/SolidWorks.Interop.swconst\~SolidWorks.Interop.swconst\_namespace.html](https://help.solidworks.com/2024/english/api/swconst/SolidWorks.Interop.swconst~SolidWorks.Interop.swconst_namespace.html)  
66. CreateSpline3 Method (ISketchManager) \- 2024 \- SOLIDWORKS ..., accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~CreateSpline3.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~CreateSpline3.html)  
67. What's New In the 2024 SOLIDWORKS API \- CADSharp.com, accessed May 16, 2025, [https://www.cadsharp.com/blog/whats-new-2024-api/](https://www.cadsharp.com/blog/whats-new-2024-api/)  
68. GetCreateFeatureErrors Method (IFeatureManager) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IFeatureManager\~GetCreateFeatureErrors.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IFeatureManager~GetCreateFeatureErrors.html)  
69. EnableFeatureTree Property (IFeatureManager) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IFeatureManager\~EnableFeatureTree.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IFeatureManager~EnableFeatureTree.html)  
70. Programming with the SOLIDWORKS API \- 2025, accessed May 16, 2025, [https://help.solidworks.com/2025/English/api/SWHelp\_List.html?id=2b090e666dff4a468d51a2f5764199a6](https://help.solidworks.com/2025/English/api/SWHelp_List.html?id=2b090e666dff4a468d51a2f5764199a6)  
71. 2024 api \- System Options and Document Properties \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapiprogguide/Overview/System\_Options\_and\_Document\_Properties.htm?format=P\&value=](https://help.solidworks.com/2024/english/api/sldworksapiprogguide/Overview/System_Options_and_Document_Properties.htm?format=P&value)  
72. System Options and Document Properties \- 2021 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2021/english/api/sldworksapiprogguide/Overview/System\_Options\_and\_Document\_Properties.htm](https://help.solidworks.com/2021/english/api/sldworksapiprogguide/Overview/System_Options_and_Document_Properties.htm)  
73. Create a Chamfer \- Solidworks Macro \- The CAD Coder, accessed May 16, 2025, [https://thecadcoder.com/solidworks-macros/create-sketch-chamfer/](https://thecadcoder.com/solidworks-macros/create-sketch-chamfer/)  
74. The best resources for learning the SOLIDWORKS API and PDM API in 2025 (paid and free), accessed May 16, 2025, [https://www.reddit.com/r/SolidWorks/comments/1hw1rc4/the\_best\_resources\_for\_learning\_the\_solidworks/](https://www.reddit.com/r/SolidWorks/comments/1hw1rc4/the_best_resources_for_learning_the_solidworks/)  
75. FeatureCut3 Method (IFeatureManager) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.ifeaturemanager\~featurecut3.html](https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.ifeaturemanager~featurecut3.html)  
76. ISimpleFilletFeatureData2 Interface Properties \- 2021 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2021/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISimpleFilletFeatureData2\_properties.html](https://help.solidworks.com/2021/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISimpleFilletFeatureData2_properties.html)  
77. 2024 api \- IRevolveFeatureData2 Interface Members, accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IRevolveFeatureData2\_members.html?format=P\&value=](https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IRevolveFeatureData2_members.html?format=P&value)  
78. ISweepFeatureData Interface Properties \- 2025 \- SOLIDWORKS API ..., accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISweepFeatureData\_properties.html](https://help.solidworks.com/2025/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISweepFeatureData_properties.html)  
79. IShellFeatureData Interface Methods \- 2021 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2021/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IShellFeatureData\_methods.html](https://help.solidworks.com/2021/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IShellFeatureData_methods.html)  
80. HoleWizard5 Method (IFeatureManager) \- 2024 \- SOLIDWORKS API ..., accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IFeatureManager\~HoleWizard5.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IFeatureManager~HoleWizard5.html)  
81. 2017 api \- ILinearPatternFeatureData Interface Members \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2017/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ILinearPatternFeatureData\_members.html?format=P\&value=](https://help.solidworks.com/2017/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ILinearPatternFeatureData_members.html?format=P&value)  
82. 2024 api \- IMirrorSolidFeatureData Interface Methods \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IMirrorSolidFeatureData\_methods.html?format=P\&value=](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IMirrorSolidFeatureData_methods.html?format=P&value)  
83. CreateCornerRectangle Method (ISketchManager) \- 2023 ..., accessed May 16, 2025, [https://help.solidworks.com/2023/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.ISketchManager\~CreateCornerRectangle.html](https://help.solidworks.com/2023/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ISketchManager~CreateCornerRectangle.html)  
84. CreateCornerRectangle Method (ISketchManager) \- 2012 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2012/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.ISketchManager\~CreateCornerRectangle.html](https://help.solidworks.com/2012/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ISketchManager~CreateCornerRectangle.html)  
85. CreateCenterRectangle Method (ISketchManager) \- 2020 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2020/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~CreateCenterRectangle.html](https://help.solidworks.com/2020/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~CreateCenterRectangle.html)  
86. CreatePolygon Method (ISketchManager) \- 2024 \- SOLIDWORKS ..., accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.ISketchManager\~CreatePolygon.html](https://help.solidworks.com/2024/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ISketchManager~CreatePolygon.html)  
87. CreateEquationSpline2 Method (ISketchManager) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.isketchmanager\~createequationspline2.html](https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.isketchmanager~createequationspline2.html)  
88. SketchOffset2 Method (ISketchManager) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.isketchmanager\~sketchoffset2.html](https://help.solidworks.com/2023/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.isketchmanager~sketchoffset2.html)  
89. SketchUseEdge3 Method (ISketchManager) \- 2024 \- SOLIDWORKS ..., accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~SketchUseEdge3.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~SketchUseEdge3.html)  
90. SketchUseEdge3 Method (ISketchManager) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~SketchUseEdge3.html](https://help.solidworks.com/2024/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~SketchUseEdge3.html)  
91. CreateLinearSketchStepAndRep, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~CreateLinearSketchStepAndRepeat.html](https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~CreateLinearSketchStepAndRepeat.html)  
92. CreateLinearSketchStepAndRep, accessed May 16, 2025, [https://help.solidworks.com/2022/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISketchManager\~CreateLinearSketchStepAndRepeat.html](https://help.solidworks.com/2022/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISketchManager~CreateLinearSketchStepAndRepeat.html)  
93. Solidworks Macro \- Edit Circular Sketch Pattern \- The CAD Coder, accessed May 16, 2025, [https://thecadcoder.com/solidworks-macros/edit-circular-skech-pattern/](https://thecadcoder.com/solidworks-macros/edit-circular-skech-pattern/)  
94. AddRelation Method (ISketchRelationManager) \- 2024 ..., accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.ISketchRelationManager\~AddRelation.html](https://help.solidworks.com/2024/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ISketchRelationManager~AddRelation.html)  
95. 2025 api \- IDisplayDimension Interface Properties \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IDisplayDimension\_properties.html?format=P\&value=](https://help.solidworks.com/2025/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IDisplayDimension_properties.html?format=P&value)