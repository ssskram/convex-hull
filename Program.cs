using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using CsvHelper;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace getassets {
    public class TaggableAssets {
        public string assetType { get; set; }
        public string assetOID { get; set; }
        public string assetName { get; set; }
        public Shape shape { get; set; }
    }

    public class Shape {
        public List<Points> Points { get; set; }
    }

    public class Points {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    class Program {
        HttpClient client = new HttpClient ();

        List<TaggableAssets> AllAssets = new List<TaggableAssets> ();

        static async Task Main () {
            Program run = new Program ();
            await run.getParks ();
        }

        public async Task getParks () {
            var key = "QVBJQWRtaW46Y2FydGVncmFwaDE=";
            var cartegraphUrl = "https://cgweb06.cartegraphoms.com/PittsburghPA/api/v1/Classes/ParksClass?fields=Oid,CgShape,IDField";
            client.DefaultRequestHeaders.Clear ();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue ("Basic", key);
            string content = await client.GetStringAsync (cartegraphUrl);
            dynamic parks = JObject.Parse (content) ["ParksClass"];

            foreach (var item in parks) {
                var breaks = item.CgShape.Breaks.ToString ();
                if (breaks == null || breaks == "[]") {
                    TaggableAssets ta = new TaggableAssets () {
                    assetType = "Park",
                    assetOID = item.Oid,
                    assetName = item.IDField,
                    shape = item.CgShape.ToObject<Shape> ()
                    };
                    AllAssets.Add (ta);
                } else {
                    var shape = item.CgShape.ToObject<Shape> ();
                    Shape hull = new Shape ();
                    hull.Points = GetConvexHull (shape.Points);
                    TaggableAssets ta = new TaggableAssets () {
                        assetType = "Park",
                        assetOID = item.Oid,
                        assetName = item.IDField,
                        shape = hull
                    };
                    AllAssets.Add (ta);
                }
            }
        }
        public static double cross (Points O, Points A, Points B) {
            return (A.Lat - O.Lat) * (B.Lng - O.Lng) - (A.Lng - O.Lng) * (B.Lat - O.Lat);
        }

        public static List<Points> GetConvexHull (List<Points> points) {
            if (points == null)
                return null;

            if (points.Count () <= 1)
                return points;

            int n = points.Count (), k = 0;
            List<Points> H = new List<Points> (new Points[2 * n]);

            points.Sort ((a, b) =>
                a.Lat == b.Lat ? a.Lng.CompareTo (b.Lng) : a.Lat.CompareTo (b.Lat));

            // Build lower hull
            for (int i = 0; i < n; ++i) {
                while (k >= 2 && cross (H[k - 2], H[k - 1], points[i]) <= 0)
                    k--;
                H[k++] = points[i];
            }

            // Build upper hull
            for (int i = n - 2, t = k + 1; i >= 0; i--) {
                while (k >= t && cross (H[k - 2], H[k - 1], points[i]) <= 0)
                    k--;
                H[k++] = points[i];
            }

            return H.Take (k - 1).ToList ();
        }
    }
}