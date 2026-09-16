using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CRSConverter;
using SpatialTransformer;
using System;
using Microsoft.MixedReality.Toolkit.UI;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;

public class VizualizationManager: MonoBehaviour
{



    public GameObject calibrationManager;
    public TextMeshPro dialogWithUserText;
    public InteractableToggleCollection sceneSelectorInteractable;
    public PinchSlider sliderBuildingLight;


    public GameObject landmark_column;

    private string lmFile;
    private List<GameObject> landmarkGOList = new();
    private List<LandmarkColumn> landmarkList = new();

    /** Tranforamation parameters***/
    private HelmertTransformation helmertTransformation;
    private Vector3d meanSourcePoints;
    private double calibrationHeight;
    private string projectedCRS;


    private readonly float userHeigth = 1.8f;
    //156,68 Meter über Adria https://www.wien.gv.at/stadtentwicklung/stadtvermessung/geodaten/bkm/produkt.html
    private readonly float viennaBase = 156.68f;
    //GPS gives around 2.5 meter above the data given by the city (requires more verification)
    //private readonly float aproxDiffGPSvsWienStadtData = 2.7f; // difference to Orthometric
    private readonly float aproxDiffGPSvsWienStadtData = 43.52f; // difference to Elipsoidal
    private readonly float heightFrontGussHaus = 17f; // this is the gounf height in fron of TU
    private readonly HttpClient httpClient = new();

    bool isHeightRelativeToCalibration = true;
    float sceneHeightRelativeToCalibrationHeight = 0.0f;


    //This valau is used to place the geoobjects realtive to a given high (e.g. calibration from roofTop)


    public  void LoadScene()
    {


        DestroyAll();
        isHeightRelativeToCalibration = true;
        sceneHeightRelativeToCalibrationHeight = 0.0f;

        if (calibrationManager.GetComponent<CalibrationManager>().HelmertTransformation is not null)
        {
            /** Get transformation parameters **/
            helmertTransformation = calibrationManager.GetComponent<CalibrationManager>().HelmertTransformation;
            meanSourcePoints = calibrationManager.GetComponent<CalibrationManager>().MeanSourcePoints;
            calibrationHeight = calibrationManager.GetComponent<CalibrationManager>().ControlPointsSourceProjectedCRS[0].y;
            projectedCRS = calibrationManager.GetComponent<CalibrationManager>().getProjectedCRS();


        
            Debug.Log("Loading Scene: " + sceneSelectorInteractable.CurrentIndex);
            switch (sceneSelectorInteractable.CurrentIndex)
            {
                case 0:
                    isHeightRelativeToCalibration = false;
                    lmFile = "scene1";
                    LoadLandmarksColumnFromFile(lmFile);                  
                    break;
                case 1:
                    sceneHeightRelativeToCalibrationHeight = 0.0f;
                    lmFile = "scene1";
                    LoadLandmarksColumnFromFile(lmFile);
                    break;
                case 2:
                    sceneHeightRelativeToCalibrationHeight = 0.0f;
                    LoadLandmarksFromServerAsync("church");
                    break;
                case 3:
                    break;
                case 4:
                    break;
                default:
                    sceneHeightRelativeToCalibrationHeight = 0.0f;
                    lmFile = "scene1";
                    LoadLandmarksColumnFromFile(lmFile);
                    break;
            }
        }
        else {
            dialogWithUserText.text = "Not calibrated! Helmert transformation null! ";
            Debug.LogError("Not calibrated! Helmert transformation null! ");
        }

    }


    private void LoadLandmarksColumnFromFile(string fileName)
    {

        landmarkList = new List<LandmarkColumn>();
        try
        {
            landmarkList = ParserGml.LoadLandMarksColumnsFromResource(fileName);
        }
        catch (Exception e)
        {
            Debug.Log(e);

        }

        /**********Create landmarks game objects*********/
        CreateLandmarksGameObjects();
        /**********ADDING custon lM*******/
        //LandmarkColumn customLM = new LandmarkColumn(new double[2] { 48.1982723781694,16.3718873217303 },  "WGS84");
        //landmarks.Add(customLM);

    }


    private async void LoadLandmarksFromServerAsync(string type)
    {
    
        //int scneneIndex = sceneSelectorInteractable.CurrentIndex;

        dialogWithUserText.text = string.Format("Loading Landmarks for Scene {0}", sceneSelectorInteractable.CurrentIndex);
        Debug.Log("Loading Landmarks for Scene....");

        //string getUrl = $"http://127.0.0.1:5000/digitallandmarks/type/church";
        string getUrl = $"https://geoar.geo.tuwien.ac.at/digitallandmarks/type/{type}";

        Debug.Log(string.Format("GET request to {0}", getUrl));

        string getResponse = null;
        //dynamic anchors = null;

        try
        {
            getResponse = await SendGetRequest(getUrl);
            dialogWithUserText.text = "Landmarks load from Webservice";
            Debug.Log(getResponse);
         

        }
        catch (Exception e)
        {
            dialogWithUserText.text = "Fail to request landmarks from Webservice";
            Debug.LogError(e + ": Fail to request landmarks from Webservice");
        }

        ParseLandmarkGetResponse(getResponse);
       
        Debug.Log($"Total of Landmarks loaded: {landmarkList.Count}");
        dialogWithUserText.text = $"Total of Landmarks loaded: {landmarkList.Count}";
        CreateLandmarksGameObjects();

    }

    

    private void ParseLandmarkGetResponse(string getResponse)
    {

        landmarkList = new List<LandmarkColumn>();
        try
        {

            dialogWithUserText.text = "Landmarks load from GeoAR DB";
            Debug.Log(getResponse);

            JObject landmarksJSON = JsonConvert.DeserializeObject<JObject>(getResponse);
            if (landmarksJSON["features"] != null)
            {
                JArray landmarksJSONArray = (JArray)landmarksJSON["features"];
                Debug.Log($"Total of {landmarksJSONArray.Count} found");
                dialogWithUserText.text = $"Total of {landmarksJSONArray.Count} found";

                for (int i = 0; i < landmarksJSONArray.Count; i++)
                {
                    //Debug.Log(anchorsArray[i]);
                    //Debug.Log((string)anchorsArray[i]["properties"]["asa_id"]);
                    //Debug.Log((double)anchorsArray[i]["geometry"]["coordinates"][2]);
                    double[] latlon = { (double)landmarksJSONArray[i]["geometry"]["coordinates"][1], (double)landmarksJSONArray[i]["geometry"]["coordinates"][0] };
                    LandmarkColumn lm = new LandmarkColumn(latlon, "WGS84");

                    lm.LmName = (string)landmarksJSONArray[i]["properties"]["name"];
                    lm.SideLenght = (float)landmarksJSONArray[i]["properties"]["side"];
                    lm.GroundHeight = (float)landmarksJSONArray[i]["properties"]["ground_abs"];
                    lm.GroungHeightOffset = (float)landmarksJSONArray[i]["properties"]["ground_rel"];
                    lm.Height = (float)landmarksJSONArray[i]["properties"]["height"];
                    lm.SideLenght = (float)landmarksJSONArray[i]["properties"]["side"];
                    //Debug.Log(g.lat);
                    landmarkList.Add(lm);

                    Debug.Log($"Lanmark name: {lm.LmName}");
                    Debug.Log($"Lanmark ground height: {lm.GroundHeight}");

                }

            }
            else
            {
                dialogWithUserText.text = $"Empty json returner";
                Debug.Log($"Empty json returner");
            }



        }
        catch (Exception e)
        {
            dialogWithUserText.text = "Fail Deserialize SQL response. No Landmark found!";
            Debug.LogError(e + ": Fail Deserialize SQL response.");
        }
        
    }

   
  
    private void CreateLandmarksGameObjects()
    {

        dialogWithUserText.text = $"Creating {landmarkList.Count} Game Objects Landmarks";
        Debug.LogError($"Creating {landmarkList.Count} Game Objects Landmarks");

        try
        {
            dialogWithUserText.text = $"Creating {landmarkList.Count} Game Objects Landmarks";
            Debug.LogError($"Creating {landmarkList.Count} Game Objects Landmarks");

           
           Vector3d LandmarkLocationProjected;
            if (helmertTransformation != null)
            {
                //dialogWithUserText.text = "Transforming Lamdarks with Helmert transformations";
                Debug.LogError("Transforming Lamdarks with Helmert transformations");

                double[] pointWGS84 = { 0, 0 };
                double[] pointProjected;
                foreach (LandmarkColumn lm in landmarkList)
                {
                    Debug.Log(string.Format("Adding LM {0} WGS84: X(lon)={1}, Y(lat)={2}   ", lm.LmName, lm.Location[1], lm.Location[0]));

                    pointWGS84[0] = lm.Location[1];
                    pointWGS84[1] = lm.Location[0];
                    //Point2D tempWgs84 = new Point2D(GPSPointKeyValue.Value.longitude, GPSPointKeyValue.Value.latitude, CrsString.WGS84);
                    /****Convert GPS points from WGS84 to projectedCRS and add to source****/
                    pointProjected = CRSTransformer.transformPoint("WGS84", projectedCRS, pointWGS84);
                    Debug.Log(string.Format("LM {0}: X={1}, Y={2}", projectedCRS, pointProjected[0], pointProjected[1]));
                    double lmY;
                    if (isHeightRelativeToCalibration)
                    {
                        lmY = calibrationHeight + lm.GroundHeight - heightFrontGussHaus - userHeigth + sceneHeightRelativeToCalibrationHeight;

                    }
                    else
                    {
                        lmY = lm.GroundHeight + viennaBase + aproxDiffGPSvsWienStadtData - userHeigth;
                    }

                    Debug.Log("LM Height(from GPS): " + lmY);

                    LandmarkLocationProjected = new Vector3d(pointProjected[0], lmY, pointProjected[1]) - meanSourcePoints;
                    GameObject LM1Column = Instantiate(landmark_column, helmertTransformation.TransformGeo2Local(LandmarkLocationProjected.ToVector3()), Quaternion.identity);
                    LM1Column.name = lm.LmName;

                    LM1Column.GetComponent<Renderer>().material.SetColor("_Color", getColorByName(lm.Color));

                    Vector3 scaleChange = new Vector3(lm.SideLenght, lm.Height, lm.SideLenght);

                    //float groungBaseOffset = lm.GroungHeightOffset -userHeigth + sceneHeightRelativeToCalibrationHeight;
                    LM1Column.transform.localScale += scaleChange;
                    LM1Column.transform.Translate(0, (lm.Height / 2), 0);
                    Debug.Log("LM (Unity pos): " + LM1Column.transform.position.ToString() + " scale: " + LM1Column.transform.localScale.ToString());

                    landmarkGOList.Add(LM1Column);
                }
            }
            else
            {
                dialogWithUserText.text = "Helmert transformation null! Not calibrated!";
                Debug.LogError("Helmert transformation null! Not calibrated!");
            }
        }
        catch (Exception e)
        {
            dialogWithUserText.text = "Fail to create landmarks game objects";
            Debug.LogError("Fail to create landmarks game objects: " + e);
        }
    }

    private Color getColorByName(string colorName)
    {
        return colorName switch
        {
            "white" => Color.white,
            "black" => Color.black,
            "red" => Color.red,
            "magenta" => Color.magenta,
            "green" => Color.green,
            "cyan" => Color.cyan,
            "yellow" => Color.yellow,
            "blue" => Color.blue,
            _ => Color.white,
        };
    }

    public void UpdateLandMarksLight()
    {

        foreach (GameObject lm in landmarkGOList)
        {
            var lmRenderer = lm.GetComponent<Renderer>();
            if (sliderBuildingLight.SliderValue >= 0)
            {

                //Color customColor = lm.GetComponent<Renderer>().material.color;

                Color customColor = new Color(sliderBuildingLight.SliderValue, sliderBuildingLight.SliderValue, sliderBuildingLight.SliderValue, 1.0f);
               
                lmRenderer.material.SetColor("_Color", customColor);
            }

        }

    }

    internal void DestroyAll()
    {

        foreach (GameObject landmark in landmarkGOList)
            Destroy(landmark);

        landmarkGOList = new List<GameObject>();

    }

    internal void HideAll()
    {

        foreach (GameObject landmark in landmarkGOList)
            landmark.SetActive(false);
    }



    private async Task<string> SendGetRequest(string url)
    {
        HttpResponseMessage response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }


}
