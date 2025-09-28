using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace swmcp.server.Controllers
{
    public class SolidWorksController
    {
        private readonly ISldWorks? _sldWorks;

        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        public SolidWorksController()
        {
            try
            {
                Guid sldWorksGuid = new Guid("6af263bb-eb9f-4176-89e9-4f892eb0ca3d");
                GetActiveObject(ref sldWorksGuid, IntPtr.Zero, out object sldWorksObject);
                _sldWorks = (ISldWorks)sldWorksObject;
            }
            catch (COMException comEx)
            {
                // print COM exception message
                Console.WriteLine($"COM Exception: {comEx.Message}");
                // Handle case where SolidWorks is not running
                _sldWorks = null;
            }
        }

        public ISldWorks? SldWorks => _sldWorks;

        public ModelDoc2? GetActiveDocument()
        {
            if (_sldWorks == null) return null;
            return _sldWorks.ActiveDoc as ModelDoc2;
        }

        public object? GetPartInfo(ModelDoc2 doc)
        {
            if (doc == null || doc.GetType() != (int)swDocumentTypes_e.swDocPART)
            {
                return null;
            }

            var part = (PartDoc)doc;
            var bodies = (object[]?)part.GetBodies2((int)swBodyType_e.swSolidBody, true);
            if (bodies == null || bodies.Length == 0)
            {
                return null;
            }
            var body = bodies[0] as Body2;
            if (body == null)
            {
                return null;
            }

            return new
            {
                Path = doc.GetPathName(),
                Title = doc.GetTitle(),
                Mass = GetMass(doc),
                Features = GetFeatures(doc),
                BoundingBox = GetBoundingBox(body)
            };
        }

        private double GetMass(ModelDoc2 doc)
        {
            var massProps = doc.Extension.CreateMassProperty();
            return massProps.Mass;
        }

        private object GetFeatures(ModelDoc2 doc)
        {
            var featureManager = doc.FeatureManager;
            var features = (object[]?)featureManager.GetFeatures(false);
            var featureList = new List<object>();

            if (features == null)
            {
                return featureList;
            }

            foreach (var featureObject in features)
            {
                var feature = (Feature)featureObject;
                featureList.Add(new
                {
                    Name = feature.Name,
                    TypeName = feature.GetTypeName2()
                });
            }

            return featureList;
        }

        private object? GetBoundingBox(Body2 body)
        {
            var box = (double[]?)body.GetBodyBox();
            if (box == null) return null;
            return new
            {
                Min = new { X = box[0], Y = box[1], Z = box[2] },
                Max = new { X = box[3], Y = box[4], Z = box[5] }
            };
        }
    }
}