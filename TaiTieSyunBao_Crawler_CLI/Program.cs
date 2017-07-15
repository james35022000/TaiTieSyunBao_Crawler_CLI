using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaiTieSyunBao_Crawler_CLI
{
    class Program
    {
        class Station_Info
        {
            public string Area;
            public string Name;
            public string Longitude;
            public string Latitude;
            public string ID;
        }

        class Store_Info
        {
            public string Name;
            public string Near_Station;
            public string ID;
            public string Longitude;
            public string Latitude;
            public string[] Types;
        }

        class LatLng
        {
            public LatLng(string Latitude, string Longitude)
            {
                this.Latitude = Latitude;
                this.Longitude = Longitude;
            }
            public string Latitude;
            public string Longitude;
        }

        static string[] API_KEY = new string[10] { "AIzaSyCvCAxqYjNap7Q15t_5jwOp8kLD4qOjjjs",
                                                    "AIzaSyC1RVX8yMuP9C3RaqqYSYZ6Q-LMN4VUGXY",
                                                    "AIzaSyDxoiJtA0ZFa4JuwkVliK4oWvcxJOneqvs",
                                                    "AIzaSyBItCdRGnSiGHQSutLu9itR23dP-y3z9sA",
                                                    "AIzaSyDOK9gASkhWdAUH4asswF5_pYs2UsrVS1o",
                                                    "AIzaSyCiyZ7xixc0P5rsw6d372BvU7FjBrcozfM",
                                                    "AIzaSyCgVePBVrGeN99AB9g-WD4wHGbP7j7aAT4",
                                                    "AIzaSyBYRw18PEe9byyBVQZfiMfIpDQjfpT-XT8",
                                                    "AIzaSyBNSYUntnESPXSi6PvM85cBoBRJ8NkzjMg",
                                                    "AIzaSyD_x4JTOAooIv9S2HuGpiZby8I9cB6Vhgw" };
        static string Place_Url = "https://maps.googleapis.com/maps/api/place/nearbysearch/json?";
        static string Geocoding_URL = "https://maps.googleapis.com/maps/api/geocode/json?";
        static string Firebase_URL = "https://taitiesyunbao-d1296.firebaseio.com/";

        static double radius = 1000;
        static double earthRadiusKm = 6371;
        static string[] direction = new string[9]{ "Z", "N", "S", "W", "E", "NE", "NW", "SW", "SE" };

        static void Main(string[] args)
        {

            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                string html, jsonData;
                html = client.DownloadString("https://www.google.com/maps/search/?api=1&query=24.806657,120.975865&query_place_id=ChIJ-0zpVcM1aDQRVXWOZhBAxcw&hl=zh-TW");
                int index = html.IndexOf("cacheResponse", html.IndexOf("cacheResponse") + 1);
                jsonData = "{\"j\":" + html.Substring(index + 14, html.IndexOf(";", index) - index - 15) + "}";
                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(html);
                HtmlNodeCollection nodes = document.DocumentNode.SelectNodes("/html[1]/body[1]/jsl[1]/div[3]/div[8]/div[8]/div[1]/div[2]/div[1]/div[1]/div[1]/button[1]");
                foreach (HtmlNode node in nodes)
                {
                    Console.WriteLine(node.InnerText);
                }
            }

            /* Get stations' infomation */
            string[] stations = File.ReadAllLines(@"C:\Users\Jack-PC\Desktop\Railway.txt");
            string area = "Unknown";
            string stations_data;
            int API_COUNT = 0;
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("content-type", "application/json");
                stations_data = client.DownloadString(Firebase_URL + "Stations" + "/.json");
            }

            /*foreach (string s in stations)
            {
                string[] lines = System.IO.File.ReadAllLines(@"C:\Users\Jack-PC\Desktop\place.txt");
                using (System.IO.StreamWriter file = File.AppendText(@"C:\Users\Jack-PC\Desktop\place.txt"))
                {
                    bool b = true;
                    foreach (string line in lines)
                    {
                        if (line.Equals(s))
                        {
                            b = false;
                        }
                    }
                    if (b)
                        file.WriteLine(s);
                }
            }*/

            foreach (string s in stations)
            {
                if (s[0].Equals('['))
                {
                    area = s.Split('[')[1].Split(']')[0];
                }
                else
                {
                    try
                    {
                        JObject.Parse(stations_data)[area][s]["Name"].ToString();
                    }
                    catch
                    {
                        using (var client = new WebClient())
                        {
                            client.Encoding = Encoding.UTF8;
                            client.Headers.Add("content-type", "application/json");
                            string response = safeHttpRequest(client, Geocoding_URL + "address=" + HttpUtility.UrlEncode(s + "車站") + "&key=" + API_KEY[API_COUNT]);
                            if (JObject.Parse(response)["status"].ToString().Equals("OK"))
                            {
                                Station_Info info = new Station_Info();
                                info.Area = area;
                                info.Latitude = JObject.Parse(response)["results"][0]["geometry"]["location"]["lat"].ToString();
                                info.Longitude = JObject.Parse(response)["results"][0]["geometry"]["location"]["lng"].ToString();
                                info.Name = s;
                                info.ID = JObject.Parse(response)["results"][0]["place_id"].ToString();
                                response = client.UploadString(Firebase_URL + "Stations/" + area + "/" + s + "/.json", "PUT", JsonConvert.SerializeObject(info));
                            }
                        }
                    }
                }
            }


            /* Get stores near stations */
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("content-type", "application/json");
                stations_data = client.DownloadString(Firebase_URL + "Stations" + "/.json");
            }
            stations = File.ReadAllLines(@"C:\Users\Jack-PC\Desktop\place.txt");
            area = "Unknown";
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("content-type", "application/json");

                List<string> storeList = new List<string>();
                string response = safeHttpRequest(client, Firebase_URL + "Stores/.json");
                try
                {
                    Dictionary<string, object> values = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                    foreach (var value in values)
                        storeList.Add(value.Key);
                }
                catch
                {

                }
                foreach (string s in stations)
                {
                    Console.WriteLine();
                    Console.Write(s);
                    if (s[0].Equals('['))
                    {
                        area = s.Split('[')[1].Split(']')[0];
                    }
                    else
                    {
                        
                        string pagetoken = "";
                        for (double k = 0.5; k <= radius / 1000; k += 0.3)
                            for (int j = 0; j < 9; j++)
                                do
                                {
                                    Console.Write(".");
                                    LatLng latLng = CalculateLatLng(JObject.Parse(stations_data)[area][s]["Latitude"].ToString(), JObject.Parse(stations_data)[area][s]["Longitude"].ToString(), k, direction[j]);
                                    string location = latLng.Latitude + "," + latLng.Longitude;
                                    if (pagetoken.Equals(""))
                                    {
                                        response = safeHttpRequest(client, Place_Url + "location=" + location + "&rankby=distance" + "&type=restaurant" + "&key=" + API_KEY[API_COUNT]);
                                        while (JObject.Parse(response)["status"].ToString().Equals("OVER_QUERY_LIMIT"))
                                        {
                                            //Console.Write("Please enter new API Key：");
                                            //API_KEY = Console.ReadLine();
                                            API_COUNT++;
                                            if (API_COUNT == 10)
                                            {
                                                Console.WriteLine("NO API KEY!!");
                                                Console.Read();
                                            }
                                            response = safeHttpRequest(client, Place_Url + "location=" + location + "&rankby=distance" + "&type=restaurant" + "&key=" + API_KEY[API_COUNT]);
                                        }
                                    }
                                    else
                                    {
                                        response = safeHttpRequest(client, Place_Url + "pagetoken=" + pagetoken + "&key=" + API_KEY[API_COUNT]);
                                        while (JObject.Parse(response)["status"].ToString().Equals("OVER_QUERY_LIMIT") || JObject.Parse(response)["status"].ToString().Equals("INVALID_REQUEST"))
                                        {
                                            if (JObject.Parse(response)["status"].ToString().Equals("OVER_QUERY_LIMIT"))
                                            {
                                                //Console.Write("Please enter new API Key：");
                                                //API_KEY = Console.ReadLine();
                                                API_COUNT++;
                                                if (API_COUNT == 10)
                                                {
                                                    Console.WriteLine("NO API KEY!!");
                                                    Console.Read();
                                                }
                                                response = safeHttpRequest(client, Place_Url + "pagetoken=" + pagetoken + "&key=" + API_KEY[API_COUNT]);
                                            }
                                            else
                                            {
                                                Thread.Sleep(1000);
                                                response = safeHttpRequest(client, Place_Url + "pagetoken=" + pagetoken + "&key=" + API_KEY[API_COUNT]);
                                            }
                                        }
                                    }
                                    if (JObject.Parse(response)["status"].ToString().Equals("OK"))
                                    {
                                        try { pagetoken = JObject.Parse(response)["next_page_token"].ToString(); }
                                        catch { pagetoken = ""; }
                                        foreach (JObject item in JArray.Parse(JObject.Parse(response)["results"].ToString()))
                                        {
                                            Store_Info info = new Store_Info();
                                            info.Near_Station = s;
                                            info.Latitude = item["geometry"]["location"]["lat"].ToString();
                                            info.Longitude = item["geometry"]["location"]["lng"].ToString();
                                            if (Math.Abs(distanceEarth(double.Parse(info.Latitude), double.Parse(info.Longitude),
                                                            double.Parse(JObject.Parse(stations_data)[area][s]["Latitude"].ToString()),
                                                            double.Parse(JObject.Parse(stations_data)[area][s]["Longitude"].ToString())) * 1000) > radius)
                                            {
                                                pagetoken = "";
                                                break;
                                            }
                                            info.Name = item["name"].ToString();
                                            info.ID = item["place_id"].ToString();
                                            JArray types = JArray.Parse(item["types"].ToString());
                                            info.Types = new string[types.Count];
                                            for (int i = 0; i < types.Count; i++)
                                                info.Types[i] = types[i].ToString();

                                            if (!isStoreExist(storeList, info.ID))
                                            {
                                                client.UploadString(Firebase_URL + "Stores/" + "Stations/" + s + "/" + info.ID + "/.json", "PUT", "0");
                                                client.UploadString(Firebase_URL + "Stores/" + info.ID + "/.json", "PUT", JsonConvert.SerializeObject(info));
                                                storeList.Add(info.ID);
                                            }
                                        }
                                    }
                                    else if (JObject.Parse(response)["status"].ToString().Equals("ZERO_RESULTS"))
                                    {
                                        Console.WriteLine(JObject.Parse(response)["status"].ToString() + ":" + s);
                                    }
                                    else
                                    {
                                        Console.WriteLine(JObject.Parse(response)["status"].ToString());
                                        Console.ReadLine();
                                    }
                                } while (!pagetoken.Equals(""));
                    }
                }
                
            }
            
                Console.ReadLine();
        }

        private static string safeHttpRequest(WebClient client, string URL)
        {
            int count = 0;
            do
            {
                try
                {
                    return client.DownloadString(URL);
                }
                catch
                {
                    if(count == 5)
                    {
                        count = 0;
                        Console.WriteLine("Http Error!");
                        Console.ReadLine();
                    }
                    Thread.Sleep(1000);
                    count++;
                }
            } while (true);
        }

        private static bool isStoreExist(List<string> storeList, string place_id)
        {
            foreach (string store in storeList)
                if (store.Equals(place_id))
                    return true;
            return false;
        }

        private static LatLng CalculateLatLng(string Lat, string Lng, double distance, string direction)
        {
            double lat_diff = distance / 110.574;
            double lon_distance = 111.320 * Math.Cos(double.Parse(Lat) * Math.PI / 180);
            double lon_diff = distance / lon_distance;
            
            switch(direction)
            {
                case "N":
                    return new LatLng((double.Parse(Lat) + Math.Abs(lat_diff)).ToString(), Lng);
                case "S":
                    return new LatLng((double.Parse(Lat) - Math.Abs(lat_diff)).ToString(), Lng);
                case "E":
                    return new LatLng(Lat, (double.Parse(Lng) + Math.Abs(lon_diff)).ToString());
                case "W":
                    return new LatLng(Lat, (double.Parse(Lng) - Math.Abs(lon_diff)).ToString());
                case "NE":
                    return new LatLng((double.Parse(Lat) + Math.Abs(lat_diff)).ToString(), (double.Parse(Lng) + Math.Abs(lon_diff)).ToString());
                case "NW":
                    return new LatLng((double.Parse(Lat) + Math.Abs(lat_diff)).ToString(), (double.Parse(Lng) - Math.Abs(lon_diff)).ToString());
                case "SE":
                    return new LatLng((double.Parse(Lat) - Math.Abs(lat_diff)).ToString(), (double.Parse(Lng) + Math.Abs(lon_diff)).ToString());
                case "SW":
                    return new LatLng((double.Parse(Lat) - Math.Abs(lat_diff)).ToString(), (double.Parse(Lng) - Math.Abs(lon_diff)).ToString());
            }

            return new LatLng(Lat, Lng);
        }


        // This function converts decimal degrees to radians
        static double deg2rad(double deg)
        {
            return (deg * Math.PI / 180);
        }

        //  This function converts radians to decimal degrees
        static double rad2deg(double rad)
        {
            return (rad * 180 / Math.PI);
        }

        /**
         * Returns the distance between two points on the Earth.
         * Direct translation from http://en.wikipedia.org/wiki/Haversine_formula
         * @param lat1d Latitude of the first point in degrees
         * @param lon1d Longitude of the first point in degrees
         * @param lat2d Latitude of the second point in degrees
         * @param lon2d Longitude of the second point in degrees
         * @return The distance between the two points in kilometers
         */
        private static double distanceEarth(double lat1d, double lon1d, double lat2d, double lon2d)
        {
            double lat1r, lon1r, lat2r, lon2r, u, v;
            lat1r = deg2rad(lat1d);
            lon1r = deg2rad(lon1d);
            lat2r = deg2rad(lat2d);
            lon2r = deg2rad(lon2d);
            u = Math.Sin((lat2r - lat1r) / 2);
            v = Math.Sin((lon2r - lon1r) / 2);
            return 2.0 * earthRadiusKm * Math.Asin(Math.Sqrt(u * u + Math.Cos(lat1r) * Math.Cos(lat2r) * v * v));
        }
    }
}
