
using UnityEngine;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using Microsoft.Azure.SpatialAnchors.Unity;
using Microsoft.Azure.SpatialAnchors;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using Newtonsoft.Json.Linq;
using SpatialTransformer;

public class GeoSpatialAnchorsManager : MonoBehaviour
{

    public InteractableToggleCollection scenes;
    public GameObject calibrationManager;

    public TextMeshPro spatialAnchorsText;


    private readonly HttpClient httpClient = new();
    /// <summary>
    /// Main interface to anything Spatial Anchors related
    /// </summary>
    private SpatialAnchorManager _spatialAnchorManager = null;

    // <summary>
    /// Used to keep track of all the created Anchor IDs
    /// </summary>
    

    /// <summary>
    /// Used to keep track of all GameObjects that represent a found or created anchor
    /// </summary>
    //private List<GameObject> geoanchor_sphere_list = new List<GameObject>();
    private List<GeoAnchor> geoAnchorlist = new List<GeoAnchor>();
    private List<string> anchorIDs = new List<string>();


    private int EXPIRATION_DAYS = 90; 

    private class GeoAnchor
    {
        public string asa_id { get; set; }
        public int scene_id { get; set; }
        public int days_to_expire { get; set; }
        public bool is_geo { get; set; }
        public double lat { get; set; }
        public double lon { get; set; }
        public double height { get; set; }


    }


    // Start is called before the first frame update
    void Start()
    {
        _spatialAnchorManager = GetComponent<SpatialAnchorManager>();
        _spatialAnchorManager.LogDebug += (sender, args) => Debug.Log($"ASA - Debug: {args.Message}");
        _spatialAnchorManager.Error += (sender, args) => Debug.LogError($"ASA - Error: {args.ErrorMessage}");
        _spatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;
    }

    
    public async void SaveAnchorsAsync()
    {

        if (!_spatialAnchorManager.IsSessionStarted)
            await _spatialAnchorManager.StartSessionAsync();
        //private List<String> _createdAnchorIDs = new List<String>();
        
        //holo_sphere must have a geoAnchorScript!
        List<GameObject> geoanchor_sphere_list = calibrationManager.GetComponent<CalibrationManager>().Holo_sphere_list;

        geoAnchorlist = new();

        int i = 0;
        int scneneIndex = scenes.CurrentIndex;
        foreach (GameObject geoAnchorSphere in geoanchor_sphere_list)
        {
            

            //Add and configure ASA components
            CloudNativeAnchor cloudNativeAnchor = geoAnchorSphere.AddComponent<CloudNativeAnchor>();
            await cloudNativeAnchor.NativeToCloud();
            CloudSpatialAnchor cloudSpatialAnchor = cloudNativeAnchor.CloudAnchor;
            cloudSpatialAnchor.Expiration = DateTimeOffset.Now.AddDays(EXPIRATION_DAYS);

            //Collect Environment Data
            while (!_spatialAnchorManager.IsReadyForCreate)
            {
                float createProgress = _spatialAnchorManager.SessionStatus.RecommendedForCreateProgress;
                Debug.Log($"ASA - Move your device to capture more environment data: {createProgress:0%}");
                spatialAnchorsText.text = $"ASA - Move your device to capture more environment data: {createProgress:0%}";
            }
            
            Debug.Log($"ASA - Saving cloud anchor... ");
            spatialAnchorsText.text = $"ASA - Saving cloud anchor... ";

            try
            {
                // Now that the cloud spatial anchor has been prepared, we can try the actual save here.
                await _spatialAnchorManager.CreateAnchorAsync(cloudSpatialAnchor);

                bool saveSucceeded = cloudSpatialAnchor != null;
                if (!saveSucceeded)
                {
                    Debug.LogError($"ASA - Failed to save Geo_Anchor_Sphere {i} to the cloud. No exception was thrown.");
                    spatialAnchorsText.text = $"ASA - Failed to save Geo_Anchor_Sphere {i} to the cloud. No exception was thrown.";
                    return;
                }

                Debug.Log($"ASA - Saved Geo_Anchor_Sphere {i} to the cloud with ID: {cloudSpatialAnchor.Identifier}");
                spatialAnchorsText.text = $"ASA - Saved Geo_Anchor_Sphere {i} to the cloud";
                geoAnchorSphere.GetComponent<GeoAnchorScript>().Asa_id = cloudSpatialAnchor.Identifier;              

                geoAnchorSphere.GetComponent<MeshRenderer>().material.color = Color.red;

                GeoAnchor g = new GeoAnchor
                {
                    asa_id = geoAnchorSphere.GetComponent<GeoAnchorScript>().Asa_id,
                    scene_id = scneneIndex,
                    days_to_expire = EXPIRATION_DAYS,
                    is_geo = true,
                    lat = geoAnchorSphere.GetComponent<GeoAnchorScript>().SourceGeoCoordinateWG84.z,
                    lon = geoAnchorSphere.GetComponent<GeoAnchorScript>().SourceGeoCoordinateWG84.x,
                    height = geoAnchorSphere.GetComponent<GeoAnchorScript>().SourceGeoCoordinateWG84.y,

                };
                geoAnchorlist.Add(g);
            }
            catch (Exception exception)
            {
                Debug.Log("ASA - Failed to save anchor: " + exception.ToString());
                spatialAnchorsText.text = "ASA - Failed to save anchor: " + exception.ToString();
                Debug.LogException(exception);
            }



        }

        /*check if all anchor were save to the ASA cloud*/
        if (geoAnchorlist.Count == geoanchor_sphere_list.Count)
        {
            string jsonAnchorList = JsonConvert.SerializeObject(geoAnchorlist);
            Debug.Log(jsonAnchorList);

            // # $ curl -X POST -H "Content-type: application/json" -d "{\"asa_id\" : \"832940jdj3902sdf43xd4urhlk23u9sdf\", \"scene_id\" : 2, \"days_to_expire\" : 3, \"is_geo\" : true, \"lat\" : 12.438534,\"lon\" : 48.345435434,\"height\" : 200.1}" "localhost:5000/spatialanchors/save"

            spatialAnchorsText.text = string.Format("Saving to DB GeoSpatialAchors spheres for Scene {0}", scenes.CurrentIndex);
            Debug.Log(string.Format("Saving to DB GeoSpatialAchors spheres for Scene {0}", scenes.CurrentIndex));

            string postUrl = "https://geoar.geo.tuwien.ac.at/spatialanchors/saveall";
            //string postUrl = "http://127.0.0.1:5000/spatialanchors/saveall";
            Debug.Log(string.Format("POST request to {0}", postUrl));
            //string jsonData = "{\"asa_id\" : \"832940jdj3902sdafawef43xd4urhlk23sdasdffdsdfu9sdf\", \"scene_id\" : 204, \"days_to_expire\" : 3, \"is_geo\" : true, \"lat\" : 12.438534,\"lon\" : 48.345435434,\"height\" : 200.1}";
            //[{"asa_id":"test0","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01},{"asa_id":"test1","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01},{"asa_id":"test2","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01},{"asa_id":"test3","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01},{"asa_id":"test4","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01},{"asa_id":"test5","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01},{"asa_id":"test6","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01},{"asa_id":"test7","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01},{"asa_id":"test8","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01},{"asa_id":"test9","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01},{"asa_id":"test10","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01},{"asa_id":"test11","scene_id":1,"days_to_expire":3,"is_geo":true,"lat":45.2423432423424,"lon":5.032479472394,"height":200.01}]


            try
            {
                /*Post request return the same content is it was successful, ele it return the DB error*/
                string postResponse = await SendPostRequest(postUrl, jsonAnchorList);

                Debug.Log("POST response: " + postResponse);
                if (postResponse == "created")
                {
                    foreach (GameObject geoAnchorSphere in geoanchor_sphere_list)
                    {
                        geoAnchorSphere.GetComponent<MeshRenderer>().material.color = Color.green;
                    }
                    spatialAnchorsText.text = "Geo_anchors save to GeoAR DB";
                    Debug.LogError("Geo_anchors save to GeoAR DB: " + postResponse);
                }
                else
                {
                    spatialAnchorsText.text = "DB error: " + postResponse;
                    Debug.LogError("POST response error: " + postResponse);
                }

            }
            catch (Exception e)
            {
                spatialAnchorsText.text = "Fail to save to GeoAR DB: " + e.Message;
                Debug.LogError("Fail to save geo anchors to GeoAR DB: " + e.Message);
            }

        }
        else
        {
            spatialAnchorsText.text = "Fail to save geo anchors to ASA Cloud";
            Debug.LogError("Fail to save geo anchors to ASA Cloud");
        }


        // Stop Session Cloud Anchor Session
        _spatialAnchorManager.DestroySession();

    }


    public async void LoadAnchorsAsync()
    {

        if (_spatialAnchorManager.IsSessionStarted)
            _spatialAnchorManager.DestroySession();

        calibrationManager.GetComponent<CalibrationManager>().resetCalibration();
        geoAnchorlist = new List<GeoAnchor>();
        anchorIDs = new List<string>();

        int scneneIndex = scenes.CurrentIndex;

        spatialAnchorsText.text = string.Format("Loading GeoSpatialAchors spheres for Scene {0}", scenes.CurrentIndex);
        Debug.Log("Loading Spatial Anchors Geospheres for Scene....");

        string getUrl = $"https://geoar.geo.tuwien.ac.at/spatialanchors/{scneneIndex}";
        //string getUrl = $"http://127.0.0.1:5000/spatialanchors/{scneneIndex}";
        Debug.Log(string.Format("GET request to {0}", getUrl));

        string getResponse = null;
        //dynamic anchors = null;
        
        try
        {
            getResponse = await SendGetRequest(getUrl);
            spatialAnchorsText.text = "Geo_anchors load from GeoAR DB";
            Debug.Log(getResponse);

            JObject anchors = JsonConvert.DeserializeObject<JObject>(getResponse);
            

            try
            {

                if (anchors["features"] != null)
                {
                    JArray anchorsArray = (JArray)anchors["features"];
                    Debug.Log($"Total of {anchorsArray.Count} found, for scene {scneneIndex}");
                    spatialAnchorsText.text = $"Total of {anchorsArray.Count} found, for scene {scneneIndex}";

                    for (int i = 0; i < anchorsArray.Count; i++)
                    {
                        //Debug.Log(anchorsArray[i]);
                        //Debug.Log((string)anchorsArray[i]["properties"]["asa_id"]);
                        //Debug.Log((double)anchorsArray[i]["geometry"]["coordinates"][2]);

                        GeoAnchor g = new GeoAnchor
                        {
                            asa_id = (string)anchorsArray[i]["properties"]["asa_id"],
                            scene_id = scneneIndex,
                            days_to_expire = 0,
                            is_geo = true,
                            lat = (double)anchorsArray[i]["geometry"]["coordinates"][1],
                            lon = (double)anchorsArray[i]["geometry"]["coordinates"][0],
                            height = (double)anchorsArray[i]["geometry"]["coordinates"][2],

                        };
                        //Debug.Log(g.lat);
                        geoAnchorlist.Add(g);

                        Debug.Log($"ASA ID: {g.asa_id}");
                        anchorIDs.Add(g.asa_id);



                    }

                    Debug.Log($"Total of ASA_ID: {anchorIDs.Count}");
                    spatialAnchorsText.text = $"Total of ASA_ID: {anchorIDs.Count}";

                    try
                    {
                        LoadAnchorsASACloud();
                    }
                    catch (Exception e)
                    {
                        spatialAnchorsText.text = "Fail to Create Watcher to locate Anchors";
                        Debug.LogError(e);
                    }

                }
                else
                {
                    spatialAnchorsText.text = $"No valid Geo_anchor found in GeoAR DB for scene {scneneIndex}";
                    Debug.Log($"No valid Geo_anchor found in GeoAR DB for scene {scneneIndex}");
                }


            }
            catch (Exception e)
            {
                spatialAnchorsText.text = "Fail Deserialize SQL response. No geo Anchor found!";
                Debug.LogError("Fail Deserialize SQL response. No geo Anchor found: " + e);
            }

        }
        catch (Exception e)
        {
            spatialAnchorsText.text = "Fail to load geo anchors from GeoAR DB";
            Debug.LogError(e);
        }
   

    }

    // <LongTap>
    /// <summary>
    /// Called to start ASA cloud section
    /// </summary>
    private async void LoadAnchorsASACloud()
    {

        if (!_spatialAnchorManager.IsSessionStarted)
            await _spatialAnchorManager.StartSessionAsync();

        LocateAnchor();

    }

    /// <summary>
    /// Looking for anchors with ID in anchorIDs
    /// </summary>
    private void LocateAnchor()
    {
        Debug.Log($"Creating Watcher to find {geoAnchorlist.Count} spatial anchors");
        spatialAnchorsText.text = $"Creating Watcher to find {geoAnchorlist.Count} spatial anchors";
         if (anchorIDs.Count > 0)
        {
            //Create watcher to look for all stored anchor IDs
            
            AnchorLocateCriteria anchorLocateCriteria = new AnchorLocateCriteria();
            anchorLocateCriteria.Identifiers = anchorIDs.ToArray();
            _spatialAnchorManager.Session.CreateWatcher(anchorLocateCriteria);
            Debug.Log($"ASA - Watcher created!");
            spatialAnchorsText.text = $"ASA - Watcher created!";
        }
    }
    /// <summary>
    /// Callback when an anchor is located
    /// </summary>
    /// <param name="sender">Callback sender</param>
    /// <param name="args">Callback AnchorLocatedEventArgs</param>
    private void SpatialAnchorManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        Debug.Log($"ASA - Anchor recognized as a possible anchor {args.Identifier} {args.Status}");
        spatialAnchorsText.text = $"ASA - Anchor recognized as a possible anchor {args.Identifier} {args.Status}";

        if (args.Status == LocateAnchorStatus.Located)
        {
            //Creating and adjusting GameObjects have to run on the main thread. We are using the UnityDispatcher to make sure this happens.
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                // Read out Cloud Anchor values
                CloudSpatialAnchor cloudSpatialAnchor = args.Anchor;

                //Create GeoAnchorSpheres GameObject
                GameObject geoAnchorSphere = Instantiate(calibrationManager.GetComponent<CalibrationManager>().holo_sphere);
                geoAnchorSphere.GetComponent<MeshRenderer>().material.color = Color.red;
                geoAnchorSphere.GetComponent<GeoAnchorScript>().SourceGeoCoordinateWG84 = findGeoCoordinate(args.Identifier);


                // Link to Cloud Anchor
                geoAnchorSphere.AddComponent<CloudNativeAnchor>().CloudToNative(cloudSpatialAnchor);
                calibrationManager.GetComponent<CalibrationManager>().Holo_sphere_list.Add(geoAnchorSphere);
                int countLocatedGeoAnchorSpheres = calibrationManager.GetComponent<CalibrationManager>().Holo_sphere_list.Count;

                Debug.Log($"Located {countLocatedGeoAnchorSpheres} Geo_Anchors out of  {geoAnchorlist.Count}");
                spatialAnchorsText.text = $"Located {countLocatedGeoAnchorSpheres} Geo_Anchors out of  {geoAnchorlist.Count}";

                if(geoAnchorlist.Count == countLocatedGeoAnchorSpheres)
                {
                    spatialAnchorsText.text = $"READY TO CALIBRAE! Located {countLocatedGeoAnchorSpheres} Geo_Anchors out of  {geoAnchorlist.Count}";
                }

            });
        }
    }

    private Vector3d findGeoCoordinate(string identifier)
    {
        foreach(GeoAnchor geoAnchor in geoAnchorlist)
        {
            if(geoAnchor.asa_id == identifier)
            {
                Vector3d geoCoord = new Vector3d(geoAnchor.lon, geoAnchor.height, geoAnchor.lat);
                return geoCoord;
            }
        }
        return null;
    }

    private async Task<string> SendGetRequest(string url)
    {
        HttpResponseMessage response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> SendPostRequest(string url, string jsonData)
    {
        HttpContent content = new StringContent(jsonData);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");


        Debug.Log(content.ToString()); 
        HttpResponseMessage response = await httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

}

