# **Polling SolidWorks Part State and Projections via the.NET API for Automated Control**

## **I. Introduction**

### **A. Purpose of the Report**

This report aims to equip.NET developers with the knowledge to leverage the SolidWorks Application Programming Interface (API) for exhaustive polling of an active part document's state. This includes interrogating its fundamental properties, navigating its FeatureManager Design Tree, extracting detailed sketch information, and, crucially, generating 2D projections of the part onto various planes. The methods discussed are intended to form the backbone of a service designed for automated control and data extraction from a local SolidWorks application.

### **B. Critical Note on API Version and Documentation Access**

The primary query specifies the SolidWorks 2025.NET API documentation. However, the main landing page for the 2025 API help (https://help.solidworks.com/2025/english/api/SWHelp\_List.html) was reported as inaccessible at the time of initial research.1 Consequently, this report synthesizes information from available API documentation, primarily from SolidWorks versions 2019 through 2024 2, and established SolidWorks API best practices.

The SolidWorks API, particularly its core interfaces, generally maintains a high degree of backward compatibility and conceptual consistency across recent versions. Methods and interfaces discussed are likely to be present and functionally similar in the 2025 version. For instance, fundamental interfaces like IModelDoc2 and IFeature have been stable cornerstones of the API for many years. However, developers must consult the official SolidWorks 2025 API documentation once it becomes accessible to verify specific method signatures, parameters, and any version-specific nuances or enhancements. This due diligence is essential because, while core functionality often persists, new optional parameters or improved versions of methods (e.g., Save2 vs. Save3 4) can be introduced. Relying solely on older documentation for a newer API version without final verification can lead to unexpected behavior or missed opportunities for using more efficient or feature-rich API calls.

### **C. Target Audience and Prerequisites**

This report is intended for software developers and engineers proficient in.NET (preferably C\#) and familiar with general programming concepts. A basic understanding of SolidWorks part modeling—concepts like features, sketches, and configurations—is beneficial but not strictly required, as API interactions related to these concepts will be explained.

## **II. Establishing Connection and Accessing Part Document**

### **A. Connecting to the SolidWorks Application**

The initial step in any SolidWorks automation task is to establish a connection with the running SolidWorks application. In a.NET environment, this typically involves using COM Interop services to obtain an instance of the SolidWorks application object. While VBA examples often use CreateObject("SldWorks.Application") 5, the.NET equivalent would be methods like System.Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application")) for starting a new instance, or more commonly for interacting with an already running instance, System.Runtime.InteropServices.Marshal.GetActiveObject("SldWorks.Application"). The resulting object should be cast to the SldWorks.Application interface, commonly aliased as ISldWorks. The SolidWorks API is a COM programming interface, and the.NET interop assemblies (e.g., SolidWorks.Interop.SldWorks.dll, SolidWorks.Interop.swconst.dll), typically found in the install\_dir\\api\\redist folder, facilitate this communication.2

### **B. Retrieving the Active Part Document**

Once a reference to the ISldWorks application object (often named swApp) is secured, the currently active document can be accessed via the ISldWorks.ActiveDoc property.5 It is imperative to verify that a document is actually open before proceeding, as ActiveDoc will return null (or Nothing in VB) if no document is active. A simple null check is sufficient for this.5

The object returned by ActiveDoc is of type IModelDoc2. This is a versatile, high-level interface applicable to all SolidWorks document types: parts, assemblies, and drawings. Since the focus of this report is on part documents, the retrieved IModelDoc2 object must be subsequently verified to be a part document and then cast to the more specific IPartDoc interface to access part-specific functionalities. This type verification can be performed using the IModelDoc2.GetType() method, which returns a value from the swDocumentTypes\_e enumeration (e.g., swDocPART, swDocASSEMBLY, swDocDRAWING 5). Attempting to cast a non-part document to IPartDoc or calling part-specific methods on a generic IModelDoc2 instance that does not represent a part will lead to runtime exceptions. This check-and-cast pattern is a fundamental practice when dealing with polymorphic interfaces in COM-based APIs.

### **C. Essential IModelDoc2 and IPartDoc Properties for Initial State**

Several properties available through IModelDoc2 and IPartDoc are fundamental for polling the initial state of an opened part:

* **IModelDoc2.GetPathName()**: This method is indispensable for identifying the specific part file being interrogated. It returns a string containing the full file path, including the filename. If the document has not yet been saved, this method returns an empty string.6  
* **IModelDoc2.GetTitle()**: Retrieves the title of the document as displayed in the SolidWorks window, which may or may not include the file extension depending on SolidWorks settings.5  
* **IModelDoc2.GetType()**: As mentioned, this returns an integer from the swDocumentTypes\_e enumeration, allowing confirmation that the document is indeed a part (swDocPART).5  
* **IModelDoc2.GetActiveConfiguration()**: Returns an IConfiguration object representing the currently active configuration of the part.7 Configurations can drastically alter a part's geometry and properties, making this a critical entry point for state polling.  
* **IModelDoc2.FeatureManager**: Provides access to the IFeatureManager object, which is the gateway to traversing and querying the FeatureManager Design Tree.8  
* **IModelDoc2.Extension**: This property returns an IModelDocExtension object. This interface offers a wide array of extended functionalities, including methods for creating mass property calculation objects (CreateMassProperty() 9), accessing display state settings (GetDisplayStateSetting() 10), and performing selections programmatically (SelectByID2() 11).  
* **IPartDoc Interface**: After confirming the IModelDoc2 object represents a part and casting it to IPartDoc, numerous part-specific methods become available. These are particularly important for attributes unique to parts, such as detailed material properties.12

## **III. Comprehensive Polling of Part Attributes**

### **A. Managing and Querying Configurations**

SolidWorks configurations allow a single part file to represent multiple variations of a design. An automated polling service must be acutely aware of configurations, as they can affect geometry, custom properties, feature suppression states, and material assignments.

The primary interface for configuration management at the document level is IConfigurationManager, accessible via IModelDoc2.ConfigurationManager. The currently active configuration can be directly obtained using IModelDoc2.GetActiveConfiguration() or IConfigurationManager.GetActiveConfiguration(), both of which return an IConfiguration object.7

To poll all configurations, one can retrieve a list of their names using IConfigurationManager.GetConfigurationNames() and then iterate through this list, obtaining each IConfiguration object via IConfigurationManager.GetConfigurationByName(configName).14 For each IConfiguration object, several properties are of interest:

* IConfiguration.Name: The name of the configuration.14  
* IConfiguration.IsDerived(): A boolean indicating if the configuration is derived from another.14  
* Hierarchy information: IConfiguration.GetChildrenCount(), IConfiguration.GetChildren() (returns an array of child IConfiguration objects), and IConfiguration.GetParent() (for derived configurations) allow for understanding the configuration tree structure.14

Configuration-specific custom properties are a vital part of a part's state. These are accessed via the ICustomPropertyManager object associated with each IConfiguration, obtained by calling IConfiguration.CustomPropertyManager.7

* ICustomPropertyManager.GetAll3(out names, out types, out values, out resolved, out linkProp): Retrieves all custom properties for that configuration, providing their names, data types (as swCustomInfoType\_e), resolved string values, a flag indicating if the value was resolved (e.g., from a linked property), and a flag indicating if it's a linked property.7  
* ICustomPropertyManager.Get6(PropertyName, UseCached, out ValOut, out ResolvedValOut, out WasResolved, out LinkToProp): Fetches details for a single, named custom property.7  
* ICustomPropertyManager.GetType2(PropertyName): Returns the swCustomInfoType\_e for a specific property.7

The ability of configurations to fundamentally change a part means that any "current state" polling must be contextualized by the active configuration. A service might need to iterate through all configurations or allow specification of a target configuration to provide a complete picture. Without this awareness, polled data (like mass or even feature existence) could be misleading.

### **B. Retrieving Material Properties**

Material assignment is a key physical property of a part and can also be configuration-specific. After obtaining an IPartDoc interface (by casting IModelDoc2), material properties can be queried:

* IPartDoc.GetMaterialPropertyName2(ConfigurationName, out MaterialDatabasePath): This method returns the assigned material's name for the specified ConfigurationName and provides the path to the material database (.sldmat file) in the MaterialDatabasePath output parameter.13 This is the recommended method for ensuring configuration-specific material data is retrieved.  
* IPartDoc.MaterialIdName: This property (get/set) provides a more structured string identifying the material: "Database name|Material name|Material database ID".12 This can be useful for uniquely identifying materials across different SolidWorks installations or custom databases if the IDs are managed.  
* Visual properties linked to materials can be accessed using IPartDoc.GetMaterialVisualProperties() and modified with IPartDoc.SetMaterialVisualProperties().13 These methods deal with the appearance aspects (color, texture, RealView settings) associated with the material.

Given that material properties can vary per configuration (e.g., one configuration might be "Steel Alloy A" and another "Aluminum 6061"), it is crucial to use methods like GetMaterialPropertyName2 that explicitly take a configuration name as an argument to ensure the polled data accurately reflects the intended design variation.

### **C. Accessing Mass Properties**

Calculating mass properties is a common requirement for understanding a part's physical characteristics. The SolidWorks API provides the IMassProperty interface for this purpose.

1. Obtain an IModelDocExtension object from IModelDoc2.Extension.  
2. Call IModelDocExtension.CreateMassProperty() to instantiate an IMassProperty object.9

The IMassProperty object allows access to:

* Mass  
* Center of Mass (X, Y, Z coordinates)  
* Principal Axes of Inertia  
* Principal Moments of Inertia  
* Moments of Inertia taken about a specified coordinate system.

The calculation can be tailored:

* **Body Scope:** IMassProperty.AddBodies(Bodies) or IMassProperty.IAddBodies(Bodies) can be used to specify an array of IBody2 objects for which the mass properties should be calculated. If these methods are not called, the calculation includes all solid bodies in the part document.9 This allows for calculations on sub-components or specific regions if the part contains multiple disjoint bodies.  
* **Coordinate System:** IMassProperty.SetCoordinateSystem(Origin, XAxisVector, YAxisVector) allows specification of a custom coordinate system for the calculation of moments of inertia. If not set, the document's origin is used by default.9  
* **Units:** IMassProperty.UseSystemUnits can be set to true to use system-defined units (typically meters, kilograms, seconds internally, though dialogs may show user preferences). If false, user-defined document units are used. The API documentation should be consulted for precise unit handling by specific IMassProperty properties (e.g., IMassProperty.Mass returns grams by default if UseSystemUnits is true 9).

The flexibility to define the scope of bodies and the reference coordinate system means that "mass properties" are not a single static set of values. An automated service might need to compute these properties under various conditions, such as for specific bodies or relative to a dynamically defined coordinate system, rather than just relying on the part's overall default values.

### **D. Querying Display States and Visual Properties**

The visual representation of a part is also part of its state. The API provides methods to query how the part is displayed in the active view and potentially how appearances are managed through display states.

* The IModelView object, typically obtained via IModelDoc2.ActiveView 18, represents the current viewport.  
* IModelView.GetDisplayState(swViewDisplayType\_e displayType): This method returns a boolean indicating the status of various view-level display settings. The swViewDisplayType\_e enumeration includes members like swIsViewShaded, swIsViewWireFrame, swIsViewHiddenLinesRemoved, swIsViewPerspective, etc..18 This provides a general overview of the active view's rendering style.

For more granular control, particularly regarding appearances that might be linked to named display states within the part (similar to how assemblies use them for component appearances), the IDisplayStateSetting interface can be explored. While example 1010 focuses on assembly components, parts can also have display states that control the appearance (color, transparency, texture) of bodies or features.

1. Obtain IModelDocExtension from IModelDoc2.Extension.  
2. IModelDocExtension.GetDisplayStateSetting(swDisplayStateOpts\_e option) returns an IDisplayStateSetting object. swThisDisplayState refers to the active one, while swSpecifyDisplayState allows targeting named display states.10  
3. If using swSpecifyDisplayState, IDisplayStateSetting.Names (an array of strings) is used to list the target display states.  
4. IDisplayStateSetting.Entities would then be set with the specific part bodies or features whose display properties are being queried (if the API supports this for parts in the same way it does for assembly components).  
5. Methods like IModelDocExtension.DisplayMode(displayStateSettingObject), IModelDocExtension.Transparency(displayStateSettingObject), and IModelDocExtension.Visibility(displayStateSettingObject) could then potentially be used to get component-specific (or feature/body-specific in a part context) visual states for the specified display states.10

The applicability of the IDisplayStateSetting mechanism for querying feature/body appearances within *part* display states needs verification in the SolidWorks 2025 API documentation, as existing examples primarily demonstrate its use with assembly components. However, the underlying concept of named display states affecting visual properties exists for parts too.

**Table 1: Core Interfaces for Part State Acquisition**

| Interface Name | Key Purpose for State Polling | Example.NET Methods/Properties (Illustrative) |
| :---- | :---- | :---- |
| ISldWorks | Accessing the SolidWorks application instance, active document. | ActiveDoc, GetModeler() |
| IModelDoc2 | General document operations, access to managers, configurations. | GetPathName(), GetTitle(), GetType(), GetActiveConfiguration(), FeatureManager, Extension, ActiveView |
| IPartDoc | Part-specific operations, material properties. | GetMaterialPropertyName2(), MaterialIdName, GetBodies2() |
| IConfigurationManager | Managing and accessing all configurations in a document. | GetConfigurationNames(), GetConfigurationByName(), GetActiveConfiguration() |
| IConfiguration | Accessing data for a specific configuration. | Name, CustomPropertyManager, IsDerived(), GetChildren(), GetParent() |
| ICustomPropertyManager | Reading and writing custom properties for a configuration/document. | GetAll3(), Get6(), GetType2() |
| IMassProperty | Calculating mass, center of mass, moments of inertia. | Mass, CenterOfMass, AddBodies(), SetCoordinateSystem() |
| IModelView | Querying and controlling view orientation and display style. | Orientation3, Translation3, Scale2, GetDisplayState() |
| IModelDocExtension | Accessing extended document functionalities. | CreateMassProperty(), GetDisplayStateSetting(), SelectByID2(), NeedsRebuild |

## **IV. Interrogating the FeatureManager Design Tree**

The FeatureManager Design Tree is the ordered list of operations (features) that construct the SolidWorks part. Polling its state involves traversing this tree and extracting data from each feature.

### **A. Accessing IFeatureManager**

The entry point for interacting with the feature tree is the IFeatureManager interface, obtained from the IModelDoc2.FeatureManager property.8 While IFeatureManager offers properties to control the visual display of the tree in the SolidWorks UI (e.g., ViewDependencies, ViewFeatures, ShowFeatureDetails, ShowHierarchyOnly 8), these are generally less relevant for a background data polling service unless it also needs to manipulate the UI state for some reason. For pure data extraction, the primary use of IFeatureManager is implicitly as the owner/manager of the features themselves.

### **B. Iterative Traversal of Features**

Traversing the FeatureManager Design Tree requires navigating its hierarchical structure. Features can be top-level or nested as sub-features (e.g., a sketch feature is a sub-feature of an extrude feature; features can be grouped in folders).

A common traversal pattern involves:

1. Starting with the first top-level feature using IModelDoc2.FirstFeature() \[19, adapted from 19's VBA model.FirstFeature()\].  
2. Iterating through sibling features using IFeature.GetNextFeature(). This method returns the next feature at the same level in the tree, or null if there are no more siblings.  
3. For each feature encountered, checking for sub-features using IFeature.GetFirstSubFeature().  
4. If sub-features exist, recursively applying the traversal logic starting from the first sub-feature and using IFeature.GetNextSubFeature() to iterate through sibling sub-features.

The VBA macro GetFeatures presented in an external resource 19 exemplifies a robust recursive approach to collect all features, including sub-features. This logic can be directly translated to.NET (e.g., C\#) using recursive methods or an iterative approach with a stack to manage parent features. A simple linear traversal using only GetNextFeature() will invariably miss nested features, leading to an incomplete poll of the part's construction. Therefore, handling this hierarchy is paramount for comprehensive feature tree polling.

### **C. Extracting Feature Data**

Once an IFeature object is obtained during traversal, various methods allow extraction of its defining data:

* **IFeature.Name**: Returns the user-visible name of the feature as it appears in the FeatureManager Design Tree.19  
* **IFeature.GetTypeName2()**: This is a critical method that returns a string identifying the type of the feature (e.g., "Sketch", "Boss-Extrude", "Cut-Extrude", "RefPlane", "Fillet", "Chamfer", "HoleWzd").3 This type name is essential for determining how to further process the feature and what kind of definition or specific interface to expect.  
* **IFeature.GetDefinition()**: This method returns a feature-specific data object that holds the parameters and settings used to create the feature. The actual type of the returned object depends on the feature's type (e.g., for an extrude feature, it might return an IExtrudeFeatureData2 object; for a reference plane, an IRefPlaneFeatureData object). This is the primary way to access the parametric definition of most features.3  
* **IFeature.GetSpecificFeature2()**: For certain feature types, the API provides a more specialized interface that offers direct methods and properties beyond the generic definition data. GetSpecificFeature2() attempts to return this more specific interface (e.g., for a "Sketch" feature, it returns an ISketch object; for a "RefPlane" feature, an IRefPlane object). If no such specific interface exists for a given feature type (common for many constructive solid features like extrusions, lofts, sweeps, fillets), this method returns null.3  
* **IFeature.GetFaces()**: Returns an array of IFace2 objects that are "owned" by this feature. It's important to understand that a single face in the model can be the result of, or be modified by, multiple features. This method returns all faces for which the current feature is considered an owner.23 This differs from faces highlighted in the UI on selection, which might be filtered.

The interplay between GetTypeName2(), GetDefinition(), and GetSpecificFeature2() is key to effective feature data extraction. A typical workflow is:

1. Call IFeature.GetTypeName2() to identify the feature's category.  
2. Based on this type name, anticipate whether a specific interface is available.  
3. Call IFeature.GetSpecificFeature2().  
4. If it returns a non-null object, cast it to the expected specific interface (e.g., ISketch, IRefPlane) and use its members.  
5. If GetSpecificFeature2() returns null, then call IFeature.GetDefinition() and cast the result to the appropriate feature data object (e.g., IExtrudeFeatureData2, IFilletFeatureData2) to access its parameters. This conditional logic is fundamental because many common feature types (like extrusions or fillets) do not have a "specific feature" interface beyond their definition data object.

### **D. Identifying Key Feature Types for Polling**

For a comprehensive state poll, certain feature types are often of particular interest:

* **Sketches**: Identified by type name (commonly "Sketch" or similar, verify with GetTypeName2()). Use IFeature.GetSpecificFeature2() to obtain the ISketch interface for detailed geometry and status.  
* **Reference Planes**: Identified by type name (e.g., "RefPlane"). Use IFeature.GetSpecificFeature2() to get IRefPlane. Standard planes (Front, Top, Right) are also features in the tree and can be accessed this way, in addition to specific methods for standard planes.  
* **Boss/Base/Cut Features**: These include extrusions, revolutions, sweeps, lofts, cuts, etc. (e.g., type names "Boss-Extrude", "Cut-Thin", "Sweep"). Use IFeature.GetDefinition() to get their respective data objects (e.g., IExtrudeFeatureData2, ISweepFeatureData).  
* **Projected Curves**: Often have a generic feature type like "REFCURVE" or a specific name like "ProjectedCurve". The definition is obtained via IFeature.GetDefinition(), which returns a ProjectionCurveFeatureData object. This object's AccessSelections() method is then used to determine the source sketch and target faces/plane.20  
* **Origin**: The part origin is typically represented as a feature (e.g., type name "OriginProfileFeature").

**Table 2: Feature Tree Interrogation API**

| API Object/Interface | Method/Property | Description for Feature Tree Tasks | .NET Usage Notes (C\# Example Context) |
| :---- | :---- | :---- | :---- |
| IModelDoc2 | FirstFeature() | Gets the first feature in the FeatureManager design tree. | IFeature swFeat \= swModel.FirstFeature(); |
| IFeatureManager | (Properties) | Controls tree display (e.g., ViewFeatures). Less critical for pure data polling. | swModel.FeatureManager.ViewFeatures \= true; |
| IFeature | GetNextFeature() | Gets the next sibling feature. | IFeature nextFeat \= swFeat.GetNextFeature(); |
| IFeature | GetFirstSubFeature() | Gets the first child feature. | IFeature subFeat \= swFeat.GetFirstSubFeature(); |
| IFeature | GetNextSubFeature() | Gets the next sibling sub-feature. | IFeature nextSubFeat \= subFeat.GetNextSubFeature(); |
| IFeature | Name | Gets the display name of the feature. | string featName \= swFeat.Name; |
| IFeature | GetTypeName2() | Gets the feature type string (e.g., "Sketch", "Boss-Extrude"). Crucial for identification. | string featType \= swFeat.GetTypeName2(); |
| IFeature | GetDefinition() | Gets the feature's definition data object (type varies by feature). | object featDef \= swFeat.GetDefinition(); // Cast to specific type, e.g., IExtrudeFeatureData2 |
| IFeature | GetSpecificFeature2() | Gets a more specific interface if available (e.g., ISketch). Returns null otherwise. | object specificFeat \= swFeat.GetSpecificFeature2(); // Cast to ISketch, IRefPlane etc. if not null |
| IFeature | GetFaces() | Gets an array of IFace2 objects owned by the feature. | object faces \= (object)swFeat.GetFaces(); |

## **V. Extracting Detailed Sketch Information**

Sketches form the 2D foundation for most 3D features in SolidWorks. Polling their state involves accessing their geometry, relations, and definition status.

### **A. Obtaining ISketch from IFeature**

As outlined in the feature traversal section, once a feature has been identified as a sketch (e.g., its IFeature.GetTypeName2() returns "Sketch"), the IFeature.GetSpecificFeature2() method is called. If successful (returns non-null), the returned System.Object should be cast to ISketch.3 This ISketch interface is the primary means of querying detailed information about that specific sketch. A null check before casting is essential.

### **B. Accessing Sketch Geometry**

The ISketch interface provides methods to retrieve the geometric entities that constitute the sketch:

* **ISketch.GetSketchPoints2()**: Returns a variant array of all ISketchPoint objects within the sketch.24 Each ISketchPoint represents a point entity.  
* **ISketch.GetSketchSegments()**: Returns a variant array of ISketchSegment objects.24 ISketchSegment is a base interface for all linear and curved entities like lines, arcs, ellipses, splines, etc.

While the ISketchManager interface (obtained via IModelDoc2.SketchManager) is used for *creating* new sketch entities (e.g., ISketchManager.CreateLine, ISketchManager.CreateArc 21), for *querying* the contents of an *existing* sketch, the methods of the ISketch object itself are used.

### **C. Interpreting Sketch Entity Properties and Coordinates**

The raw coordinates obtained from sketch entities are relative to the sketch's own 2D coordinate system, which is defined by its sketch plane.

* **ISketchPoint**:  
  * Provides X, Y, and Z properties representing its coordinates.26 Within the sketch's local 2D system, Z is typically 0\.  
* **ISketchSegment**:  
  * This is a base interface. To get detailed geometric data, it must be cast to one of its derived types, such as ISketchLine, ISketchArc, ISketchEllipse, or ISketchSpline.  
  * ISketchSegment.ConstructionGeometry (boolean): Indicates if the segment is for construction purposes only.  
  * ISketchLine: Methods like GetStartPoint2() and GetEndPoint2() return the start and end ISketchPoint objects.  
  * ISketchArc: Provides methods to get its center point (ISketchPoint), radius, start angle, and end angle.  
  * ISketchSpline: Offers access to its definition data, including control points and knots. Relatedly, ISplineParamData.GetControlPoints() describes how control point coordinates are structured based on dimension and periodicity.27

A crucial aspect of interpreting sketch coordinates is understanding their frame of reference. The coordinates retrieved directly from ISketchPoint objects are in the sketch's local 2D (X,Y) space. The Z-coordinate is typically zero relative to this plane. To transform these local sketch coordinates into the part's global 3D coordinate system, or vice-versa, the sketch's transformation matrix must be used. The ISketch interface provides ModelToSketchTransform (an IMathTransform that transforms from model space to sketch space) and SketchToModelTransform (an IMathTransform for the inverse operation). Applying the SketchToModelTransform to local sketch points will yield their positions in the global 3D space of the part. This transformation is essential for accurately locating sketch geometry within the overall part or for projecting it onto other planes.

### **D. Sketch Relations and State**

Beyond geometry, other aspects of a sketch's state can be polled:

* **Geometric Relations**: ISketch.GetSketchRelationsCount() and ISketch.GetSketchRelations() (which returns an array of ISketchRelation objects) can be used to query the constraints applied between sketch entities (e.g., coincident, parallel, perpendicular, tangent).  
* **Definition Status**: ISketch.GetStatus() returns a value from the swSketchSolveStatus\_e enumeration, indicating whether the sketch is Under Defined, Fully Defined, Over Defined, or has other statuses. This is important for understanding the robustness and stability of the sketch.  
* While various system options affect sketch behavior (e.g., "Use fully defined sketches," "Display arc centerpoints" 28), these are generally global settings rather than the polled "state" of an individual sketch instance.

## **VI. Obtaining Part Projections on Different Planes**

Generating 2D projections of a 3D part onto specified planes is a key requirement. This involves understanding view orientations and then using appropriate API methods to create or extract the projected geometry.

### **A. Understanding and Determining View Orientations**

1\. Accessing the Current Model View:  
The IModelView interface represents a view in the SolidWorks graphics area. It can be accessed for the currently active view using IModelDoc2.ActiveView.18 If multiple views are open (e.g., viewports), IModelDoc2.GetFirstModelView() and IModelView.GetNext() can be used to iterate through them.29  
2\. View Orientation Matrix (MathTransform):  
The orientation of a view is defined by an IMathTransform object.

* IModelView.Orientation3: This property gets or sets the model view's orientation matrix.30 This 4x4 matrix transforms coordinates from the model's space to the view's space (effectively defining the camera's orientation and position relative to the model).  
* The IMathTransform is structured as a 4x4 matrix where elements 0-8 (a 3x3 submatrix) define rotation, elements 9-11 define translation (as a vector), and element 12 defines uniform scale.32 Elements 13-15 are typically unused.  
* Complementary properties include IModelView.Translation3 (gets/sets the translation vector part of the transform) and IModelView.Scale2 (gets/sets the view scale factor).30

3\. Identifying Standard Views (Front, Top, Right, Isometric, etc.):  
Determining if the current view corresponds to a standard engineering view (like Front, Top, Right, Isometric, Trimetric, Dimetric) requires comparing its orientation matrix to known matrices for these standard views.

* The swStandardViews\_e enumeration lists identifiers for these standard views (e.g., swFrontView, swTopView, swIsometricView).33  
* While IModelDoc2.ShowNamedView2(ViewName, ViewId) can be used to *set* the active view to a standard one using either its name (e.g., "\*Front", "\*Top", "\*Isometric" 34) or its swStandardViews\_e ID 36, there is no direct API method like IModelView.GetCurrentStandardViewType() that returns the swStandardViews\_e enum for the current view.  
* To ascertain if the current IModelView.Orientation3 matches a standard view, a comparison is necessary: a. Obtain the IMathTransform of the current view using IModelView.Orientation3. b. Obtain reference IMathTransform objects for each standard view of interest. These can be derived: i. By temporarily setting the view to each standard view (e.g., swModel.ShowNamedView2("\*Front", \-1)), querying its IModelView.Orientation3, storing it, and then restoring the original view. This is viable but may cause screen flicker if done repeatedly for polling. ii. Using IModelDoc2.IGetStandardViewRotation(swStandardView\_e\_value). This method returns a 9-element double array representing the 3x3 rotation matrix for the specified standard view *relative to the Front view*.37 The Front view's rotation matrix (which is typically an identity matrix if the base coordinate system hasn't been altered) would also be needed. These rotation matrices can then be used to construct full IMathTransform objects (assuming standard scale and zero translation for the canonical views, or by querying these from a temporarily set view). iii. Pre-calculating or empirically determining the canonical MathTransform matrices for each standard view. An external resource 32 discusses the composition of these matrices (e.g., Top view involves a \-90 degree rotation about X-axis relative to Front view), but does not provide ready-to-use numerical matrices. c. Compare the rotation, translation, and scale components of the current view's IMathTransform with those of the reference standard view IMathTransform objects. Due to potential floating-point inaccuracies, comparisons should be done within a small tolerance. Methods like IMathTransform.IMultiply() and IMathTransform.IInverse() can be helpful in matrix algebra for these comparisons.32

This comparison process is non-trivial. The absence of a direct "get current standard view type" method means the developer must implement this logic if precise identification of the current view against standard definitions is required for deciding which projection plane to use.

### **B. Generating 2D Projections of Part Geometry**

Several approaches exist for obtaining 2D projections, ranging from querying existing projection features to generating outlines on-the-fly.

1\. Projecting Existing Curves/Sketches (via Projected Curve Feature Data):  
If a "Project Curve" feature already exists in the model, its definition and outputs can be queried:

1. Select the "Project Curve" feature (e.g., using IModelDocExtension.SelectByID2(), type "REFCURVE" or the specific feature name).  
2. Obtain the IFeature object for this selection.  
3. Call IFeature.GetDefinition() to retrieve the ProjectionCurveFeatureData object.20  
4. Use ProjectionCurveFeatureData.AccessSelections(ModelDoc2, Component2) to enable access to the entities used to define the projection.  
5. The ProjectionCurveFeatureData.Sketch property provides the source ISketch that was projected.  
6. ProjectionCurveFeatureData.GetFaceArrayCount() and ProjectionCurveFeatureData.GetFaceArray() list the target faces onto which the sketch was projected. If projected onto a plane, this would be reflected in the type of selection.  
7. The output of the "Project Curve" feature is itself a new curve or set of curves in the model. These resultant curves can be accessed as geometry of the "Project Curve" feature itself (e.g., by iterating its edges if it forms a body, or by specific methods if it produces wireframe geometry).

2\. Generating Silhouette Edges (Per-Face Approach):  
This method calculates silhouette edges for individual faces based on a specified view direction.

1. Iterate through all relevant faces of the part. This typically involves getting all bodies using IPartDoc.GetBodies2(swBodyType\_e.swSolidBody, true), then for each IBody2 object, calling IBody2.GetFaces() to get an array of IFace2 objects.  
2. For each IFace2 object: a. Define a RootPoint (double array of 3, e.g., model origin or face centroid) and a DirectionVector (double array of 3, representing the normal of the projection plane, viewed from infinity). b. Call IFace2.IGetSilhoutteEdgeCount(RootPoint, DirectionVector) to determine the number of silhouette edges for that face from that viewpoint. This is necessary to size the array for the next call. c. Call IFace2.IGetSilhoutteEdges(RootPoint, DirectionVector). This returns a variant array of IEdge objects.39 The output array structure is packed: for each edge, there are two elements, the first being the IEdge and the second unused.  
3. The returned edges are transient (not added to the model's B-rep) and cannot be selected directly.

To obtain a silhouette for the entire part using this method, one would need to:

* Define the overall projection direction (e.g., normal to the Front plane).  
* Iterate over all faces of the part.  
* For each face, determine if it's visible from the projection direction.  
* Call IGetSilhoutteEdges for visible faces using the projection direction.  
* Collect all returned IEdge objects.  
* These edges are 3D edges. Further processing would be needed to project them onto the desired 2D plane and connect them into continuous loops if a 2D wireframe is the goal. This approach is computationally intensive and requires significant post-processing.

3\. Obtaining Whole-Body 2D Outlines (IModeler.GetBodyOutline2):  
This method is often the most direct way to get the 2D outline of one or more solid bodies projected onto a plane.

1. Obtain an IModeler interface instance from ISldWorks.GetModeler().  
2. Prepare the parameters for IModeler.GetBodyOutline2(BodyList, Direction, Tolerance, ProjectOnPlane, out Curves, out TopolEntities, out Outline) 40:  
   * BodyList: An array of IBody2 objects for which to generate the outline. These can be obtained from IPartDoc.GetBodies2(swBodyType\_e.swSolidBody, true).  
   * Direction: An IMathVector object specifying the direction of view. This vector is normal to the conceptual projection plane, pointing from the object towards the viewpoint (or from viewpoint towards object, API docs should clarify convention, usually it's view direction). For standard views:  
     * Front view (looking along \+Z in typical CAD setup): Vector might be (0, 0, 1).  
     * Top view (looking along \-Y): Vector might be (0, \-1, 0\) or (0, 1, 0\) depending on convention.  
     * Right view (looking along \-X): Vector might be (-1, 0, 0\) or (1, 0, 0). These vectors can be created using IMathUtility.CreateVector(New Double(){x, y, z}).41 The IMathUtility object is obtained from ISldWorks.GetMathUtility().  
   * Tolerance: A double for geometric calculation tolerance (e.g., Parasolid default 1×10−5).  
   * ProjectOnPlane: A boolean. **Set this to True** to ensure the output curves are projected onto a plane perpendicular to the Direction vector.  
   * Curves (output): A variant array that will be populated with ICurve objects representing the outline segments. Even with ProjectOnPlane \= true, these are 3D ICurve objects, but they will all be coplanar.  
   * TopolEntities (output): A variant array of topological entities (e.g., vertices, edges) from the original bodies that correspond to the outline curves.  
   * Outline (output): A variant array of integers that group the Curves into distinct loops (e.g., outer boundary, inner hole boundaries).

Processing GetBodyOutline2 Output for 2D Coordinates:  
The Curves array from GetBodyOutline2 (with ProjectOnPlane \= true) contains 3D ICurve objects that lie on an implicit projection plane. To convert these into usable 2D coordinates relative to a specific plane (e.g., the Front, Top, or Right plane of the model, or an arbitrary plane):

1. **Define the Target 2D Coordinate System:**  
   * The Z-axis of this 2D system is effectively defined by the Direction vector used in GetBodyOutline2.  
   * An origin for this 2D system must be established on the projection plane (e.g., by projecting the 3D model origin onto this plane).  
   * X-axis and Y-axis vectors for this 2D system must be defined on the projection plane, orthogonal to each other and to the Direction vector. Standard vector algebra (cross products) can be used. For example, if projecting onto the XY plane (Direction \= (0,0,1)), the 2D X-axis could be (1,0,0) and Y-axis (0,1,0).  
2. Transform 3D Curve Points to the 2D System:  
   For each ICurve in the Curves array:  
   a. Tessellate the ICurve into a series of 3D points. ICurve.GetTessPts(tolerance, out startParam, out endParam) can provide these points.  
   b. For each 3D point (x,y,z) obtained from tessellation:  
   i. Construct an IMathTransform that maps coordinates from the global 3D space to the local 2D coordinate system of the projection plane. This transform effectively re-orients and re-positions the geometry so the projection plane becomes a canonical plane (e.g., Z=0 plane).  
   ii. Apply this transform to the 3D point. The resulting transformed point's X' and Y' coordinates are the 2D coordinates in the projection plane's system. The Z' coordinate should be zero (or very close, within tolerance).  
   c. These 2D points can then be used to reconstruct the outline in a 2D environment or to create 2D sketch entities on a SolidWorks plane.

Alternatively, if the goal is to create a 2D sketch on an *existing SolidWorks plane feature* (e.g., the "Front Plane" feature) and the Direction vector used for GetBodyOutline2 was aligned with this plane's normal:

1. Activate or select the target plane feature.  
2. Insert a new sketch on this plane: ISketchManager.InsertSketch(true).  
3. Obtain the ISketch.ModelToSketchTransform for this new sketch. This transform maps global 3D model coordinates to the 2D coordinate system of the sketch.  
4. For each 3D ICurve from GetBodyOutline2: a. Apply the ModelToSketchTransform to the curve's geometry (e.g., by transforming its tessellated points). b. Use the transformed 2D points to create corresponding 2D sketch segments (ISketchManager.CreateLine, CreateArc, etc.) within the active sketch. The examples for GetBodyOutline2 43 primarily demonstrate creating a *3D sketch* from the output curves. Converting these coplanar 3D curves into true 2D sketch entities on a specific SolidWorks plane requires these additional transformation and sketch creation steps. This post-processing is a non-trivial but essential part of obtaining usable 2D projection data.

4\. (Advanced) Creating Temporary Drawing Views for 2D Geometry Extraction:  
This is an indirect but potentially powerful method that leverages SolidWorks' drawing generation engine.

1. Programmatically create a new, temporary drawing document (ISldWorks.NewDocument(templatePath, paperSize, width, height) with a drawing template).  
2. Insert a drawing view of the part onto a sheet using IDrawingDoc.CreateDrawViewFromModelView3(ModelPathName, ViewName, x, y, z). The ViewName can be a standard view name like "\*Front", "\*Top", "\*Isometric".34  
3. The returned IView object (representing the drawing view) contains 2D geometric entities (lines, arcs, splines) that represent the projection.  
4. Methods like IView.GetVisibleEntities(out objectTypes, out objects) or IView.GetPolylines7(...) 46 can be used to extract these 2D entities. The coordinates will be relative to the drawing sheet or view origin. Example 4947 shows extracting temporary axes data from drawing views, which involves similar principles of iterating view contents.  
5. After extracting the 2D data, the temporary drawing document can be closed without saving.

This approach is more resource-intensive as it involves document creation and view generation. However, it might be advantageous if the projection requires features handled well by the drawing engine, such as specific hidden line removal styles, section views, or if GetBodyOutline2 does not provide the desired fidelity or type of output.

**Table 3: API Methods for Generating Part Projections**

| Projection Goal | Primary API Method(s) | Key Input Parameters | Nature of Output | Brief.NET Usage Notes & Post-processing |
| :---- | :---- | :---- | :---- | :---- |
| Query Existing Projected Curve | IFeature.GetDefinition(), ProjectionCurveFeatureData | Feature selection | ProjectionCurveFeatureData object, source ISketch, target IFace2 array. Resultant curve is a feature. | Cast definition to ProjectionCurveFeatureData. Use AccessSelections(). Query geometry of the feature itself. |
| Silhouette Edges (Per-Face) | IFace2.IGetSilhoutteEdges() | RootPoint (double), DirectionVector (double) for the view. | Array of transient 3D IEdge objects for a single face. | Iterate all part faces. Aggregate edges. DirectionVector is projection plane normal. Edges are 3D; further projection/transformation needed for 2D. |
| Whole-Body Projected Outline | IModeler.GetBodyOutline2() | BodyList (IBody2), Direction (IMathVector), ProjectOnPlane \= true | Array of coplanar 3D ICurve objects. Outline array groups curves into loops. | Direction is projection plane normal. Output curves are 3D but lie on projection plane. Requires transformation to a 2D coordinate system for 2D coordinates. Can be used to create 3D sketch or, with more work, 2D sketch on a specific plane. |
| (Advanced) Via Temporary Drawing View | IDrawingDoc.CreateDrawViewFromModelView3(), IView methods | ModelPathName, ViewName (e.g., "\*Front"), location on sheet. | IView object containing 2D drawing entities (lines, arcs). Coordinates relative to sheet/view. | Heavier approach. Extract 2D entities using IView.GetVisibleEntities() or similar. Manage temporary document lifecycle. |

## **VII. Considerations for an Automated Control Service**

Building a robust service for automated control of SolidWorks requires attention to how the API is used, especially concerning updates, performance, error handling, and threading.

### **A. Event-Driven Updates vs. Periodic Polling**

The query focuses on "polling" the current state. While polling at intervals is straightforward to implement, it might not be efficient for a responsive service, potentially consuming unnecessary resources or missing rapid changes between poll cycles. SolidWorks API provides event notifications that can be more suitable for triggering actions when specific changes occur.

* For example, IPartDocEvents interface offers events like FeaturePostUpdateNotify (when a feature is modified) or RegenPostNotify2 (after a model rebuild). IModelDoc2Events offers FileSaveNotify, DestroyNotify, etc.  
* Subscribing to relevant events allows the service to react to changes as they happen, rather than continuously querying. A hybrid approach could also be effective: use events as triggers, and upon an event, perform a detailed poll of the specific data that might have changed. The choice between pure polling, pure event-driven, or a hybrid model has significant architectural implications for the service's responsiveness, resource utilization, and complexity. For a service that needs to reflect the SolidWorks state with low latency, incorporating events is highly recommended.

### **B. Performance Optimization for API Calls**

Interacting with a COM-based API like SolidWorks' from.NET involves overhead for each cross-process or interop call. To maintain performance, especially in a polling service:

* **Minimize Call Frequency and Granularity:** Retrieve data in larger chunks if the API allows, rather than making many fine-grained calls. For instance, ICustomPropertyManager.GetAll3() is preferable to getting custom properties one by one in a loop if all are needed.7  
* **Check Model State:** Before performing operations that depend on an up-to-date model, check IModelDoc2.Extension.NeedsRebuild. If it returns true, the model data might be stale.  
* **Rebuilds:** IModelDoc2.ForceRebuild3(false) can be used to ensure the model is current, but rebuilds can be time-consuming. Use this method judiciously, only when necessary.  
* **Selections:** Programmatic selection using methods like IModelDocExtension.SelectByID2() 11 can impact performance if used excessively in loops. If data can be accessed directly from feature or model objects without prior selection, that is generally preferred. Some API methods, however, explicitly require entities to be selected beforehand (e.g., some sketch creation or feature data access examples 21).  
* **Suppressing Updates:** For batch operations or intensive polling sequences, temporarily disabling UI updates might offer performance gains. IFeatureManager.EnableFeatureTree \= false and IModelView.EnableGraphicsUpdate \= false can suppress FeatureManager tree and graphics window redraws, respectively. It's crucial to restore their original states after the operation. IModelView.SuppressWaitCursorDuringRedraw can prevent the hourglass cursor from flashing during API-driven redraws, improving user experience if the service interacts with a visible SolidWorks session.48

### **C. Robust Error Handling and Managing API States**

Interactions with the SolidWorks API can fail for various reasons, and a robust service must handle these gracefully:

* **Check Return Values:** Many API methods return boolean success flags or objects that can be null. Always check these return values. For example, IModelDoc2.Save3 returns a boolean and provides error/warning codes via output parameters.4 IFeature.GetSpecificFeature2() returning null is a documented behavior for certain feature types.  
* **COM Exceptions:** API calls can throw COM exceptions. These should be caught using try-catch blocks, specifically System.Runtime.InteropServices.COMException.  
* **Modal States:** SolidWorks can enter modal states (e.g., an open PropertyManager Page for a feature, a dialog box). API calls may be blocked or behave unpredictably during such states. The service should ideally ensure SolidWorks is in a receptive state or be prepared to handle failures due to modality.  
* **Application Busy State:** The SolidWorks application itself might be busy with user interactions or internal processing. While there isn't a direct "IsBusy" API property, repeated failures or timeouts in API calls might indicate this.

### **D. Threading Considerations**

This is a critical aspect for any service designed to interact with SolidWorks. The SolidWorks API is, for the most part, not thread-safe and expects to be called from the main thread where the SolidWorks application UI runs (a Single-Threaded Apartment or STA thread).

* If the automation service is multi-threaded (common for services that perform background tasks or respond to external requests), direct calls to the SolidWorks API from worker threads are highly likely to cause instability, errors (like RPC\_E\_SERVERCALL\_RETRYLATER or RPC\_E\_WRONG\_THREAD), or crashes.  
* All interactions with SolidWorks API objects must be marshalled or delegated to the main STA thread. For a.NET service, this might involve using a synchronization context captured from the main thread, or a dedicated thread that initializes COM for STA and processes API requests serially. Failure to adhere to these COM threading rules is a common source of difficult-to-diagnose problems in automation applications. A service architecture must explicitly account for this.

## **VIII. Conclusion and Recommendations**

### **A. Summary of Critical API Interfaces and Methods**

This report has detailed numerous SolidWorks API interfaces and methods crucial for comprehensively polling the state of an opened part document and its 2D projections. Key interfaces include:

* ISldWorks: For application-level access.  
* IModelDoc2: For general document properties, configurations, and access to managers like IFeatureManager and IModelDocExtension.  
* IPartDoc: For part-specific data like material properties and body access.  
* IFeatureManager and IFeature: For navigating the FeatureManager Design Tree and extracting feature definitions and types.  
* ISketch: For detailed interrogation of sketch geometry, relations, and status.  
* IModelView and IMathTransform: For understanding view orientations and transformations.  
* IModeler: Particularly its GetBodyOutline2 method, for generating projected outlines of part bodies.

These interfaces provide the tools to access document metadata, configuration details, material and mass properties, the full feature hierarchy with parametric data, sketch geometry, and to derive various forms of 2D projections.

### **B. Best Practices for Developing a Stable and Efficient SolidWorks Automation Service**

Developing a robust automation service requires careful consideration of API usage patterns and application architecture:

1. **Prioritize Official Documentation:** Once the SolidWorks 2025 API documentation becomes accessible, it should be the primary reference to confirm method signatures, new functionalities, and any version-specific behaviors.  
2. **Configuration Awareness:** Ensure all polling logic correctly handles part configurations, as they significantly impact the part's state.  
3. **Strategic Data Retrieval:** Choose API methods that balance detail with performance. For example, retrieve data in batches where possible and avoid unnecessary selections.  
4. **Manage Model State:** Be mindful of the model's rebuild state (NeedsRebuild) and use rebuilds (ForceRebuild3) only when necessary.  
5. **Robust Error Handling:** Implement comprehensive error checking for API return values and handle COM exceptions gracefully.  
6. **Threading Model:** Strictly adhere to COM STA threading rules. All SolidWorks API calls must originate from or be correctly marshalled to the main SolidWorks thread.  
7. **Event-Driven Architecture:** Consider incorporating SolidWorks events to trigger state updates rather than relying solely on periodic polling for better responsiveness and efficiency.  
8. **Iterative Development and Testing:** Given the complexity of CAD automation, develop and test modules incrementally, focusing on specific aspects of state polling (e.g., feature tree traversal, then projection generation).  
9. **Resource Management:** While.NET's Runtime Callable Wrapper (RCW) system generally handles COM object release, be mindful of scenarios that might require explicit release (e.g., extensive looping with object creation, or when using non-SolidWorks COM objects).  
10. **Logging:** Implement thorough logging within the service for diagnostic purposes, capturing API call sequences, parameters, and any errors encountered.

By applying these principles and leveraging the API methods discussed, developers can build powerful and reliable.NET-based services for the automated control and comprehensive state polling of SolidWorks part documents.

#### **Works cited**

1. accessed January 1, 1970, [https://help.solidworks.com/2025/english/api/SWHelp\_List.html](https://help.solidworks.com/2025/english/api/SWHelp_List.html)  
2. Welcome \- 2025 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/sldworksapiprogguide/Welcome.htm](https://help.solidworks.com/2025/english/api/sldworksapiprogguide/Welcome.htm)  
3. GetSpecificFeature2 Method (IFeature) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IFeature\~GetSpecificFeature2.html](https://help.solidworks.com/2023/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IFeature~GetSpecificFeature2.html)  
4. Save3 Method (IModelDoc2) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IModelDoc2\~Save3.html](https://help.solidworks.com/2023/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IModelDoc2~Save3.html)  
5. Get Document Information Example (VBA) \- 2024 \- SOLIDWORKS ..., accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/get\_document\_information\_example\_vb.htm](https://help.solidworks.com/2024/english/api/sldworksapi/get_document_information_example_vb.htm)  
6. GetPathName Method (IModelDoc2) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.imodeldoc2\~getpathname.html](https://help.solidworks.com/2023/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.imodeldoc2~getpathname.html)  
7. Get Custom Properties for Configuration Example (VBA) \- 2022 ..., accessed May 16, 2025, [https://help.solidworks.com/2022/english/api/sldworksapi/get\_custom\_properties\_for\_configuration\_example\_vb.htm](https://help.solidworks.com/2022/english/api/sldworksapi/get_custom_properties_for_configuration_example_vb.htm)  
8. Get and Set FeatureManager Design Tree Display (VBA) \- 2022 ..., accessed May 16, 2025, [https://help.solidworks.com/2022/english/api/sldworksapi/Get\_and\_Set\_FeatureManager\_Design\_Tree\_Display\_Example\_vb.htm](https://help.solidworks.com/2022/english/api/sldworksapi/Get_and_Set_FeatureManager_Design_Tree_Display_Example_vb.htm)  
9. Mass Properties \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/English/api/sldworksapiprogguide/Overview/Mass\_Properties.htm](https://help.solidworks.com/2023/English/api/sldworksapiprogguide/Overview/Mass_Properties.htm)  
10. Get Display State Settings Example (VBA) \- 2024 \- SOLIDWORKS ..., accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/Get\_Display\_State\_Settings\_Example\_VB.htm](https://help.solidworks.com/2024/english/api/sldworksapi/Get_Display_State_Settings_Example_VB.htm)  
11. How to work with selections in the SOLIDWORKS API (part 8), accessed May 16, 2025, [https://cadbooster.com/how-to-work-with-selections-in-the-solidworks-api/](https://cadbooster.com/how-to-work-with-selections-in-the-solidworks-api/)  
12. MaterialIdName Property (IPartDoc) \- 2022 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2022/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IPartDoc\~MaterialIdName.html](https://help.solidworks.com/2022/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IPartDoc~MaterialIdName.html)  
13. SetMaterialPropertyName2 Method (IPartDoc) \- 2023 ..., accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.ipartdoc\~setmaterialpropertyname2.html](https://help.solidworks.com/2023/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.ipartdoc~setmaterialpropertyname2.html)  
14. Work with Configurations Example (VBA) \- 2022 \- SOLIDWORKS ..., accessed May 16, 2025, [https://help.solidworks.com/2022/english/api/sldworksapi/work\_with\_configurations\_example\_vb.htm](https://help.solidworks.com/2022/english/api/sldworksapi/work_with_configurations_example_vb.htm)  
15. Get Material Example (VBA) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/get\_material\_example\_vb.htm](https://help.solidworks.com/2023/english/api/sldworksapi/get_material_example_vb.htm)  
16. Get and Set Material Visual Properties Example (C\#) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/sldworksapi/Get\_and\_Set\_Material\_Visual\_Properties\_Example\_CSharp.htm](https://help.solidworks.com/2024/English/api/sldworksapi/Get_and_Set_Material_Visual_Properties_Example_CSharp.htm)  
17. Mass Properties \- 2021 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2021/english/api/sldworksapiprogguide/Overview/Mass\_Properties.htm](https://help.solidworks.com/2021/english/api/sldworksapiprogguide/Overview/Mass_Properties.htm)  
18. Get Display State Example (VBA) \- 2021 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2021/English/api/sldworksapi/Get\_Display\_State\_Example\_VB.htm](https://help.solidworks.com/2021/English/api/sldworksapi/Get_Display_State_Example_VB.htm)  
19. Find features in the tree by type and/or name pattern using ..., accessed May 16, 2025, [https://www.codestack.net/solidworks-api/document/features-manager/find-features/](https://www.codestack.net/solidworks-api/document/features-manager/find-features/)  
20. Get Projected Curve Feature Data Example (C\#) \- 2024 ..., accessed May 16, 2025, [https://help.solidworks.com/2024/English/api/sldworksapi/Get\_Projected\_Curve\_Feature\_Data\_Example\_CSharp.htm](https://help.solidworks.com/2024/English/api/sldworksapi/Get_Projected_Curve_Feature_Data_Example_CSharp.htm)  
21. Get Projected Curve Feature Data Example (VBA) \- 2022 ..., accessed May 16, 2025, [https://help.solidworks.com/2022/english/api/sldworksapi/Get\_Projected\_Curve\_Feature\_Data\_Example\_VB.htm](https://help.solidworks.com/2022/english/api/sldworksapi/Get_Projected_Curve_Feature_Data_Example_VB.htm)  
22. GetSpecificFeature2 Method (IFeature) \- 2023 \- SOLIDWORKS API ..., accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IFeature\~GetSpecificFeature2.html](https://help.solidworks.com/2023/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IFeature~GetSpecificFeature2.html)  
23. GetFaces Method (IFeature) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.ifeature\~getfaces.html](https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.ifeature~getfaces.html)  
24. Get Sketch Points Example (VBA) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/Get\_Sketch\_Points\_Example\_VB.htm](https://help.solidworks.com/2023/english/api/sldworksapi/Get_Sketch_Points_Example_VB.htm)  
25. ISketchManager Interface Methods \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.isketchmanager\_methods.html](https://help.solidworks.com/2024/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.isketchmanager_methods.html)  
26. Get Sketch Points Example (VBA) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/English/api/sldworksapi/Get\_Sketch\_Points\_Example\_VB.htm](https://help.solidworks.com/2023/English/api/sldworksapi/Get_Sketch_Points_Example_VB.htm)  
27. GetControlPoints Method (ISplineParamData) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISplineParamData\~GetControlPoints.html](https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISplineParamData~GetControlPoints.html)  
28. System Options \> Sketch \- 2021 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2021/english/api/swconst/so\_sketch.htm](https://help.solidworks.com/2021/english/api/swconst/so_sketch.htm)  
29. GetFirstModelView Method (IModelDoc2) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IModelDoc2\~GetFirstModelView.html](https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDoc2~GetFirstModelView.html)  
30. Orientation3 Property (IModelView) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IModelView\~Orientation3.html](https://help.solidworks.com/2023/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IModelView~Orientation3.html)  
31. IModelView Interface Properties \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/solidworks.interop.sldworks\~solidworks.interop.sldworks.imodelview\_properties.html](https://help.solidworks.com/2023/english/api/sldworksapi/solidworks.interop.sldworks~solidworks.interop.sldworks.imodelview_properties.html)  
32. A complete overview of matrix transformations in the SOLIDWORKS ..., accessed May 16, 2025, [https://cadbooster.com/complete-overview-of-matrix-transformations-in-the-solidworks-api/](https://cadbooster.com/complete-overview-of-matrix-transformations-in-the-solidworks-api/)  
33. Standard Views Toolbar \- 2021 \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2021/English/SolidWorks/sldworks/r\_standard\_views\_toolbar\_2.htm](https://help.solidworks.com/2021/English/SolidWorks/sldworks/r_standard_views_toolbar_2.htm)  
34. CreateDrawViewFromModelView3 Method (IDrawingDoc) \- 2023 ..., accessed May 16, 2025, [https://help.solidworks.com/2023/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IDrawingDoc\~CreateDrawViewFromModelView3.html](https://help.solidworks.com/2023/English/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IDrawingDoc~CreateDrawViewFromModelView3.html)  
35. swStandardViews\_e Enumeration \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/swconst/SolidWorks.Interop.swconst\~SolidWorks.Interop.swconst.swStandardViews\_e.html](https://help.solidworks.com/2024/english/api/swconst/SolidWorks.Interop.swconst~SolidWorks.Interop.swconst.swStandardViews_e.html)  
36. ShowNamedView2 Method (IModelDoc2) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IModelDoc2\~ShowNamedView2.html](https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDoc2~ShowNamedView2.html)  
37. IGetStandardViewRotation Method (IModelDoc2) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/English/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IModelDoc2\~IGetStandardViewRotation.html](https://help.solidworks.com/2023/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDoc2~IGetStandardViewRotation.html)  
38. IMathTransform Interface Methods \- 2022 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2022/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IMathTransform\_methods.html](https://help.solidworks.com/2022/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IMathTransform_methods.html)  
39. IGetSilhoutteEdges Method (IFace2) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IFace2\~IGetSilhoutteEdges.html](https://help.solidworks.com/2023/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IFace2~IGetSilhoutteEdges.html)  
40. GetBodyOutline2 Method (IModeler) \- 2021 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2021/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks\~SOLIDWORKS.Interop.sldworks.IModeler\~GetBodyOutline2.html](https://help.solidworks.com/2021/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IModeler~GetBodyOutline2.html)  
41. CreateVector Method (IMathUtility) \- 2021 api \- SolidWorks Web Help, accessed May 16, 2025, [https://help.solidworks.com/2021/English/api/draftsightapi/Interop.dsAutomation\~Interop.dsAutomation.IMathUtility\~CreateVector.html?format=P\&value=](https://help.solidworks.com/2021/English/api/draftsightapi/Interop.dsAutomation~Interop.dsAutomation.IMathUtility~CreateVector.html?format=P&value)  
42. CreateVector Method (IMathUtility) \- 2024 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2024/english/api/draftsightapi/Interop.dsAutomation\~Interop.dsAutomation.IMathUtility\~CreateVector.html](https://help.solidworks.com/2024/english/api/draftsightapi/Interop.dsAutomation~Interop.dsAutomation.IMathUtility~CreateVector.html)  
43. Get Curves that Form Outline of Bodies Example (VBA) \- 2025 ..., accessed May 16, 2025, [https://help.solidworks.com/2025/english/api/sldworksapi/Get\_Curves\_that\_Form\_Outline\_of\_Bodies\_Example\_VB.htm](https://help.solidworks.com/2025/english/api/sldworksapi/Get_Curves_that_Form_Outline_of_Bodies_Example_VB.htm)  
44. Get Body Outline Example (VB.NET) \- 2022 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2022/English/api/sldworksapi/Get\_Body\_Outline\_Example\_VBNET.htm](https://help.solidworks.com/2022/English/api/sldworksapi/Get_Body_Outline_Example_VBNET.htm)  
45. Get Body Outline Example (VBA) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/English/api/sldworksapi/Get\_Body\_Outline\_Example\_VB.htm](https://help.solidworks.com/2023/English/api/sldworksapi/Get_Body_Outline_Example_VB.htm)  
46. ISilhouetteEdge Interface \- 2019 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2019/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.ISilhouetteEdge.html](https://help.solidworks.com/2019/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISilhouetteEdge.html)  
47. Get Temporary Axes in Each Drawing View Example (VBA) \- 2023 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/Get\_Temporary\_Axes\_in\_Each\_Drawing\_View\_Example\_VB.htm](https://help.solidworks.com/2023/english/api/sldworksapi/Get_Temporary_Axes_in_Each_Drawing_View_Example_VB.htm)  
48. SuppressWaitCursorDuringRedr, accessed May 16, 2025, [https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks\~SolidWorks.Interop.sldworks.IModelView\~SuppressWaitCursorDuringRedraw.html](https://help.solidworks.com/2023/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelView~SuppressWaitCursorDuringRedraw.html)  
49. Get Temporary Axes in Each Drawing View Example (VBA) \- 2025 \- SOLIDWORKS API Help, accessed May 16, 2025, [https://help.solidworks.com/2025/English/api/sldworksapi/Get\_Temporary\_Axes\_in\_Each\_Drawing\_View\_Example\_VB.htm](https://help.solidworks.com/2025/English/api/sldworksapi/Get_Temporary_Axes_in_Each_Drawing_View_Example_VB.htm)