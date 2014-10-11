using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using TransLoc_v1.Resources;
using System.Net.Http;
using Newtonsoft.Json;
using Windows.Devices.Geolocation;

namespace TransLoc_v1
{
    public partial class MainPage : PhoneApplicationPage
    {
        Dictionary<string, string> routeMap = new Dictionary<string, string>();
        Dictionary<string, Stop> stopMap = new Dictionary<string, Stop>();
        List<KeyValuePair<string, Stop>> stopList = new List<KeyValuePair<string, Stop>>();
        Dictionary<int, Vehicle> vehicleMap = new Dictionary<int, Vehicle>();

        public static Geoposition currentPosition;
        HttpClient client;
        public string agency = "176";

        // Constructor
        public MainPage()
        {
            InitializeComponent();
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-Mashape-Authorization", "5W3PkLlkFJEHYA5xAUEm2RnC0qeayeGW");

            // Sample code to localize the ApplicationBar
            //BuildLocalizedApplicationBar();
        }

        //gets time of next arrival and updates UI
        private async Task<Arrival> ArrivalTimeAsync()
        {
            //string stop = "4117202";
            string stop;
            if (stopList.Count > 0)
            {
                stop = stopList[0].Value.StopId;
            }
            else
            {
                stop = "4117202";
            }

            agency.Replace(",", "%2C");
            stop.Replace(",", "%2C");

            //https://transloc-api-1-2.p.mashape.com/arrival-estimates.json?agencies=176&stops=4117206
            string url = "https://transloc-api-1-2.p.mashape.com/arrival-estimates.json" +
                "?agencies={0}" +
                "&stops={1}";//json URL

            string queryUrl = string.Format(url, agency, stop);
            string translocResult = await client.GetStringAsync(queryUrl);

            //Result.Text = translocResult;
            ArrivalData apiData = JsonConvert.DeserializeObject<ArrivalData>(translocResult);

            if (apiData != null)
            {
                StopArrivals currentStop = null;
                foreach (StopArrivals stopInfo in apiData.data)
                {
                    if (stopInfo.stop_id == stop)
                    {
                        currentStop = stopInfo;
                        break;
                    }
                }

                Arrival nextArrival = currentStop.arrivals[0];
                return nextArrival;
            }


            return null;
        }

        private async void PhoneApplicationPage_Loaded(object sender, RoutedEventArgs e)
        {
            Geolocator locator = new Geolocator();
            locator.DesiredAccuracyInMeters = 15;
            Result.Text = "Loading...";

            try
            {
                currentPosition = await locator.GetGeopositionAsync(TimeSpan.FromSeconds(30),
                                                                        TimeSpan.FromSeconds(5)); ;//get current position
                //update routes, vehicles, and stops in range (async
                routeMap = await getRoutesAsync();
                vehicleMap = await getVehicleStatusesAsync();
                stopMap = await getStopsInRangeAsync(
                    currentPosition.Coordinate.Latitude,
                    currentPosition.Coordinate.Longitude,
                    250);
                stopList = stopMap.ToList();//sort stops by distance from current location
                stopList.Sort(
                    delegate(KeyValuePair<string, Stop> firstPair,
                    KeyValuePair<string, Stop> nextPair)
                    {
                        return firstPair.Value.CompareTo(nextPair.Value);
                    });

                Arrival nextArrival = await ArrivalTimeAsync();//get next bus arrival and current stop
                DateTime time = DateTime.Parse(nextArrival.arrival_at);//get next arrival time

                //format display string
                string nextBus = String.Format("Next Arrival at: {0}\n"
                    + "Route ID: {1}\n"
                    + "Vehicle is {2}% full\n"
                    , time.ToShortTimeString(),
                    routeMap[nextArrival.route_id],
                    vehicleMap[Convert.ToInt32(nextArrival.vehicle_id)].load * 100);

                //update UI
                Result.Text = nextBus;
                txtHeading.Text = stopList[0].Value.Name;
            }
            catch (Exception ex)
            {
                Result.Text = ex.Message;
            }
            //getRoutesAsync(() =>
            //   {
            //       UpdateTimesAsync();
            //   });
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                Arrival nextArrival = await ArrivalTimeAsync();//get next bus arrival and current stop
                DateTime time = DateTime.Parse(nextArrival.arrival_at);//get next arrival time

                //format display string
                string nextBus = String.Format("Next Arrival at: {0}\n"
                    + "Route ID: {1}\n"
                    + "Vehicle is {2}% full\n"
                    , time.ToShortTimeString(),
                    routeMap[nextArrival.route_id],
                    vehicleMap[Convert.ToInt32(nextArrival.vehicle_id)].load * 100);

                //update UI
                Result.Text = nextBus;
                txtHeading.Text = stopList[0].Value.Name;
            }
            catch (Exception ex)
            {
                Result.Text = ex.Message;
            }
        }

        //returns dictionary of routes
        private async Task<Dictionary<string, string>> getRoutesAsync()
        {
            agency.Replace(",", "%2C");
            Dictionary<string, string> routes = new Dictionary<string, string>();

            string url = "https://transloc-api-1-2.p.mashape.com/routes.json" +
                "?agencies={0}";

            string queryUrl = string.Format(url, agency);
            string translocResult = await client.GetStringAsync(queryUrl);

            RootObject apiData = JsonConvert.DeserializeObject<RootObject>(translocResult);
            if (apiData != null)
            {
                foreach (Route r in apiData.data.routes)
                {
                    routes.Add(r.RouteId, r.LongName);
                }
            }

            return routes;

        }

        //returns dictionary of vehicle statuses
        private async Task<Dictionary<int, Vehicle>> getVehicleStatusesAsync()
        {
            string url = "http://feeds.transloc.com/3/vehicle_statuses.jsonp" + "?agencies={0}" + "&callback=?";
            Dictionary<int, Vehicle> vehicles = new Dictionary<int, Vehicle>();

            string queryUrl = string.Format(url, agency);
            string translocResult = await client.GetStringAsync(queryUrl);
            translocResult = translocResult.Substring(2, translocResult.Length - 4);

            VehicleRootObject apiData = JsonConvert.DeserializeObject<VehicleRootObject>(translocResult);
            if (apiData != null)
            {
                foreach (Vehicle v in apiData.vehicles)
                {
                    vehicles.Add(v.id, v);
                }
            }

            return vehicles;
        }

        //gets stops within range of coordinates
        private async Task<Dictionary<string, Stop>> getStopsInRangeAsync
            (double latitude, double longitude, int range)
        {
            string url = "https://transloc-api-1-2.p.mashape.com/stops.json" +
                "?agencies={0}" + "&" + "geo_area={1},{2}|{3}";

            Dictionary<string, Stop> stops = new Dictionary<string, Stop>();
            string queryUrl = string.Format(url, agency, latitude, longitude, range);
            string translocResult = await client.GetStringAsync(Uri.EscapeUriString(queryUrl));

            StopRootObject apiData = JsonConvert.DeserializeObject<StopRootObject>(translocResult);
            if (apiData != null)
            {
                foreach (Stop s in apiData.Data)
                {
                    stops.Add(s.StopId, s);
                }
            }

            return stops;
        }

        // Sample code for building a localized ApplicationBar
        //private void BuildLocalizedApplicationBar()
        //{
        //    // Set the page's ApplicationBar to a new instance of ApplicationBar.
        //    ApplicationBar = new ApplicationBar();

        //    // Create a new button and set the text value to the localized string from AppResources.
        //    ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
        //    appBarButton.Text = AppResources.AppBarButtonText;
        //    ApplicationBar.Buttons.Add(appBarButton);

        //    // Create a new menu item with the localized string from AppResources.
        //    ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
        //    ApplicationBar.MenuItems.Add(appBarMenuItem);
        //}


        //Arrival Estimate classes
        public class Arrival
        {
            public string route_id { get; set; }
            public string vehicle_id { get; set; }
            public string arrival_at { get; set; }
            public string type { get; set; }
        }

        public class StopArrivals
        {
            public List<Arrival> arrivals { get; set; }
            public string agency_id { get; set; }
            public string stop_id { get; set; }
        }

        public class ArrivalData
        {
            public int rate_limit { get; set; }
            public int expires_in { get; set; }
            public string api_latest_version { get; set; }
            public string generated_on { get; set; }
            public List<StopArrivals> data { get; set; }
            public string api_version { get; set; }
        }


        //Route Classes
        public class Route
        {
            public Route()
            {
                return;
            }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("short_name")]
            public string ShortName { get; set; }

            [JsonProperty("route_id")]
            public string RouteId { get; set; }

            [JsonProperty("color")]
            public string Color { get; set; }

            [JsonProperty("segments")]
            public string[][] Segments { get; set; }

            [JsonProperty("is_active")]
            public bool IsActive { get; set; }

            [JsonProperty("agency_id")]
            public int AgencyId { get; set; }

            [JsonProperty("text_color")]
            public string TextColor { get; set; }

            [JsonProperty("long_name")]
            public string LongName { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("is_hidden")]
            public bool IsHidden { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("stops")]
            public string[] Stops { get; set; }
        }

        public class Data
        {
            public Data()
            {

            }

            [JsonProperty("176")]
            public Route[] routes { get; set; }
        }



        public class RootObject
        {
            public RootObject()
            {

            }

            [JsonProperty("rate_limit")]
            public int RateLimit { get; set; }

            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonProperty("api_latest_version")]
            public string ApiLatestVersion { get; set; }

            [JsonProperty("generated_on")]
            public string GeneratedOn { get; set; }

            [JsonProperty("data")]
            public Data data { get; set; }

            [JsonProperty("api_version")]
            public string ApiVersion { get; set; }
        }

        //Vehicle Objects
        public class Vehicle
        {
            public int agency_id { get; set; }
            public string apc_status { get; set; }
            public string call_name { get; set; }
            public int? current_stop_id { get; set; }
            public int heading { get; set; }
            public int id { get; set; }
            public double? load { get; set; }
            public List<double> position { get; set; }
            public int route_id { get; set; }
            public int? segment_id { get; set; }
            public double speed { get; set; }
            public object timestamp { get; set; }
        }

        public class VehicleRootObject
        {
            public bool success { get; set; }
            public List<Vehicle> vehicles { get; set; }
        }

        //Stop Objects
        public class Location
        {

            [JsonProperty("lat")]
            public double Lat;

            [JsonProperty("lng")]
            public double Lng;
        }

        public class Stop : IComparable
        {

            [JsonProperty("code")]
            public string Code;

            [JsonProperty("description")]
            public string Description;

            [JsonProperty("url")]
            public string Url;

            [JsonProperty("parent_station_id")]
            public object ParentStationId;

            [JsonProperty("agency_ids")]
            public string[] AgencyIds;

            [JsonProperty("station_id")]
            public object StationId;

            [JsonProperty("location_type")]
            public string LocationType;

            [JsonProperty("location")]
            public Location Location;

            [JsonProperty("stop_id")]
            public string StopId;

            [JsonProperty("routes")]
            public string[] Routes;

            [JsonProperty("name")]
            public string Name;

            public int CompareTo(Object obj)
            {
                Stop other = (Stop)obj;
                double lat2 = currentPosition.Coordinate.Latitude;
                double lon2 = currentPosition.Coordinate.Longitude;
            
                double lat1 = this.Location.Lat;
                double lon1 = this.Location.Lng;

                double distance1 = Math.Sqrt(Math.Pow(lat2 - lat1, 2) + 
                    Math.Cos(lat1) * Math.Pow(lon2 - lon1, 2));

                lat1 = other.Location.Lat;
                lon1 = other.Location.Lng;

                double distance2 = Math.Sqrt(Math.Pow(lat2 - lat1, 2) +
                    Math.Cos(lat1) * Math.Pow(lon2 - lon1, 2));

                return (int)((distance1 - distance2) * 1000000);
            }
        }

        public class StopRootObject
        {

            [JsonProperty("rate_limit")]
            public int RateLimit;

            [JsonProperty("expires_in")]
            public int ExpiresIn;

            [JsonProperty("api_latest_version")]
            public string ApiLatestVersion;

            [JsonProperty("generated_on")]
            public string GeneratedOn;

            [JsonProperty("data")]
            public Stop[] Data;

            [JsonProperty("api_version")]
            public string ApiVersion;
        }
    }


    }