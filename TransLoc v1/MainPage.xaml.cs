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
        Dictionary<int, Vehicle> vehicleMap = new Dictionary<int, Vehicle>();
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

        private async Task UpdateTimesAsync(Dictionary<string, string> routes)
        {
            string stop = "4117202";

            agency.Replace(",", "%2C");
            stop.Replace(",", "%2C");

            //https://transloc-api-1-2.p.mashape.com/arrival-estimates.json?agencies=176&stops=4117206
            string url = "https://transloc-api-1-2.p.mashape.com/arrival-estimates.json" +
                "?agencies={0}"+
                "&stops={1}";

            string queryUrl = string.Format(url, agency, stop);
            string translocResult = await client.GetStringAsync(queryUrl);

            //Result.Text = translocResult;
            ArrivalData apiData = JsonConvert.DeserializeObject<ArrivalData>(translocResult);

            if (apiData != null)
            {
                StopArrivals currentStop = null;
                foreach(StopArrivals stopInfo in apiData.data)
                {
                    if (stopInfo.stop_id == stop)
                    {
                        currentStop = stopInfo;
                        break;
                    }
                }

                Arrival nextArrival = currentStop.arrivals[0];
                DateTime time = DateTime.Parse(nextArrival.arrival_at);

                Result.Text = "Loading...";

                string nextBus = String.Format("Next Arrival at: {0}\n"
                    + "Route ID: {1}\n"
                    + "Vehicle is {2}% full\n"
                    ,time.ToShortTimeString(), 
                    routeMap[nextArrival.route_id], 
                    vehicleMap[Convert.ToInt32(nextArrival.vehicle_id)].load * 100);

                Result.Text = nextBus;
            }
        }

        private async void PhoneApplicationPage_Loaded(object sender, RoutedEventArgs e)
        {
            Geolocator locator = new Geolocator();
            locator.DesiredAccuracyInMeters = 10;

            try
            {
                Geoposition position = await locator.GetGeopositionAsync();

                routeMap = await getRoutesAsync();
                vehicleMap = await getVehicleStatusesAsync();
                /*await getStopsInRangeAsync(
                    (float) position.Coordinate.Latitude, 
                    (float)position.Coordinate.Longitude, 
                    100);*/
                await UpdateTimesAsync(routeMap);
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
                routeMap = await getRoutesAsync();
                vehicleMap = await getVehicleStatusesAsync();
                await UpdateTimesAsync(routeMap);
            }
            catch (Exception ex)
            {
                Result.Text = ex.Message;
            }
        }

        private async Task<Dictionary<string, string>> getRoutesAsync ()
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

        private async Task<Dictionary<int, Vehicle>> getVehicleStatusesAsync ()
        {
            string url = "http://feeds.transloc.com/3/vehicle_statuses.jsonp"+ "?agencies={0}" +"&callback=?";
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

        private async Task<Dictionary<int, Stop>> getStopsInRangeAsync 
            (float latitude, float longitude, int range)
        {
            string url = "https://transloc-api-1-2.p.mashape.com/routes.json" +
                "?agencies={0}" + "&" +"geo_area={1},{2}|{3}";
            
            Dictionary<int, Stop> stops = new Dictionary<int, Stop>();
            string queryUrl = string.Format(url, agency, latitude, longitude, range);
            string translocResult = await client.GetStringAsync(Uri.EscapeUriString(queryUrl));

            StopRootObject apiData = JsonConvert.DeserializeObject<StopRootObject>(translocResult);
            if (apiData != null)
            {
                foreach (Stop s in apiData.data)
                {
                    stops.Add(Convert.ToInt32(s.stop_id), s);
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
    }

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
        public Route ()
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
        public Data ()
        {

        }

        [JsonProperty("176")]
        public Route[] routes { get; set; }
    }



    public class RootObject
    {
        public RootObject ()
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

 //Location Objects
    public class Location
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class Stop
    {
        public string code { get; set; }
        public string description { get; set; }
        public string url { get; set; }
        public object parent_station_id { get; set; }
        public List<string> agency_ids { get; set; }
        public object station_id { get; set; }
        public string location_type { get; set; }
        public Location location { get; set; }
        public string stop_id { get; set; }
        public List<string> routes { get; set; }
        public string name { get; set; }
        //public float distance { get; set; }
    }

    public class StopRootObject
    {
        public int rate_limit { get; set; }
        public int expires_in { get; set; }
        public string api_latest_version { get; set; }
        public string generated_on { get; set; }
        public List<Stop> data { get; set; }
        public string api_version { get; set; }
    }
    }