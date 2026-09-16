using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using PositionCollector;
using System;
using SpatialTransformer;
using CRSConverter;
using TMPro;
using System.Threading.Tasks;
using Microsoft.MixedReality.Toolkit.UI;

public class CalibrationManager : MonoBehaviour
{
    public InteractableToggleCollection calibrationType;
    public InteractableToggleCollection fixedPointSet;
    public Interactable clickedButton;
    public PinchSlider sliderSquareLength;
    public Interactable checkBoxDebuggingIndoor;
    public Interactable checkBoxFakeCoords;

    public GameObject calibration_sphere;
    public GameObject holo_sphere;
    public GameObject geo_sphere;
    public GameObject fixed_sphere;

    public GameObject antena;
    public TextMeshPro calibrationText;
    public Interactable checkBoxAntenaFixedPos;

    public GameObject vizualizationManager;

    public AudioClip collectedAudio;


    private List<GameObject> calibration_spheres_list = new List<GameObject>();
    private List<GameObject> holo_sphere_list = new List<GameObject>();
    private List<GameObject> geo_sphere_list = new List<GameObject>();


    private List<Vector3d> controlPointsSourceProjectedCRS = new List<Vector3d>();
    private List<Vector3> controlPointsTargetUnity = new List<Vector3>();
    private List<Vector3d> controlPointsWGS84 = new List<Vector3d>();/*This is used only for Spatial Anchors spheres*/
    private bool accuracySpheresInstantiated = false;

    /* This variable is required because of shifting in the helmert transformation */
    private Vector3d meanSourcePoints = null;

    private HelmertTransformation helmertTransformation = null;
    // Start is called before the first frame update

    

    private float sphereYGap = 0.4f;

    private string projectedCRS = "EPSG31256";
    //private string projectedCRS = "UTM33_N";
    private bool useFakeCoordinates = false;


    private Dictionary<System.DateTime, GPSPoint> GPSPoints = new Dictionary<System.DateTime, GPSPoint>();

    public HelmertTransformation HelmertTransformation { get => helmertTransformation; set => helmertTransformation = value; }
    public Vector3d MeanSourcePoints { get => meanSourcePoints; set => meanSourcePoints = value; }
    public List<Vector3d> ControlPointsSourceProjectedCRS { get => controlPointsSourceProjectedCRS; set => controlPointsSourceProjectedCRS = value; }
    public List<GameObject> Holo_sphere_list { get => holo_sphere_list; set => holo_sphere_list = value; }

    void Start()
    {
        /****Hide profiel CPU Manager****/
        Microsoft.MixedReality.Toolkit.CoreServices.DiagnosticsSystem.ShowProfiler = false;

    }


    public void Calibrate()
    {
        // Log the current calibration type
        Debug.Log("Calibration Type: " + calibrationType.CurrentIndex);

        // Determine the calibration type and execute corresponding method
        switch (calibrationType.CurrentIndex)
        {
            // Location Based calibration
            case 0:
                Debug.Log("Starting Location-based calibration ");
                startCalibrationGPS();
                break;

            // Fixed points calibration
            case 1:
                Debug.Log("Starting fixed points calibration ");
                setTransformationFixedCalibration();
                break;

            // Spatial Anchors calibration
            case 2:
                Debug.Log("Calibration with Spatial Anchors ");
                setTransformationSpatialAnchors();
                break;

            // Default to Location-based calibration if index is out of range
            default:
                Debug.Log("Starting Location-based calibration ");
                startCalibrationGPS();
                break;
        }
    }

    // Calletd to Reset Calibration
    public void resetCalibration()
    {
        // Destroy calibration spheres
        foreach (GameObject sphere in calibration_spheres_list)
            Destroy(sphere);

        // Destroy holographic spheres
        foreach (GameObject sphere in holo_sphere_list)
            Destroy(sphere);

        // Destroy geographic spheres
        foreach (GameObject sphere in geo_sphere_list)
            Destroy(sphere);

        // Destroy all visualizations managed by VizualizationManager
        vizualizationManager.GetComponent<VizualizationManager>().DestroyAll();

        // Delete all geo objects with specific tags
        DestroyGameObjectsWithTag("landmark_column");
        DestroyGameObjectsWithTag("building_mesh");

        // Generate a log file with the current timestamp
        string path = Path.Combine(Application.persistentDataPath, "logFile_" + DateTime.Now.ToString("dd_MM_yyyy_hh_mm_ss") + ".txt");

        // Reset lists and flags
        calibration_spheres_list = new List<GameObject>();
        holo_sphere_list = new List<GameObject>();
        geo_sphere_list = new List<GameObject>();
        controlPointsSourceProjectedCRS = new List<Vector3d>();
        controlPointsWGS84 = new List<Vector3d>();
        controlPointsTargetUnity = new List<Vector3>();
        accuracySpheresInstantiated = false;

        // Reset transformation and coordinates
        helmertTransformation = null;
        meanSourcePoints = null;
        useFakeCoordinates = false;
    }

    // Helper method to destroy game objects with a specific tag
    private void DestroyGameObjectsWithTag(string tag)
    {
        GameObject[] gameObjects = GameObject.FindGameObjectsWithTag(tag);
        foreach (var gameObject in gameObjects)
            Destroy(gameObject);
    }


    public void addFixedPointCalibrationSpheres()
    {
        // Disable the clickedButton (assuming clickedButton is some UI element)
        clickedButton.IsEnabled = false;

        // Get the main camera's transform
        Transform camera_transform = Camera.main.transform;
        float camera_height = camera_transform.position.y;

        Vector3 sphere_red_position, sphere_green_position, sphere_blue_position, sphere_yellow_position;

        // If debugging indoor environment is toggled on
        if (checkBoxDebuggingIndoor.IsToggled)
        {
            // Define sphere positions for indoor debugging
            float square_length = 1;
            sphere_red_position = camera_transform.position + camera_transform.right * square_length / 2 + camera_transform.forward * square_length / 2;
            sphere_green_position = camera_transform.position + camera_transform.right * square_length / 2 - camera_transform.forward * square_length / 2;
            sphere_blue_position = camera_transform.position - camera_transform.right * square_length / 2 + camera_transform.forward * square_length / 2;
            sphere_yellow_position = camera_transform.position - camera_transform.right * square_length / 2 - camera_transform.forward * square_length / 2;

            // Adjust the gap between spheres for indoor environment
            sphereYGap = 1.7f;
        }
        else
        {
            // Define default sphere positions
            sphere_red_position = camera_transform.position + camera_transform.right * 0.8f + camera_transform.forward * 0.4f;
            sphere_green_position = camera_transform.position + camera_transform.right * 1 / 3 + camera_transform.forward * 1;
            sphere_blue_position = camera_transform.position - camera_transform.right * 1 / 3 + camera_transform.forward * 1;
            sphere_yellow_position = camera_transform.position - camera_transform.right * 0.8f + camera_transform.forward * 0.4f;
        }

        // Instantiate spheres and set their positions and colors
        GameObject sphere_red = Instantiate(fixed_sphere, new Vector3(sphere_red_position.x, camera_height - sphereYGap, sphere_red_position.z), Quaternion.identity);
        sphere_red.name = "fixed_sphere_red";
        sphere_red.GetComponent<Renderer>().material.SetColor("_Color", Color.red);

        GameObject sphere_green = Instantiate(fixed_sphere, new Vector3(sphere_green_position.x, camera_height - sphereYGap, sphere_green_position.z), Quaternion.identity);
        sphere_green.name = "fixed_sphere_green";
        sphere_green.GetComponent<Renderer>().material.SetColor("_Color", Color.green);

        GameObject sphere_blue = Instantiate(fixed_sphere, new Vector3(sphere_blue_position.x, camera_height - sphereYGap, sphere_blue_position.z), Quaternion.identity);
        sphere_blue.name = "fixed_sphere_blue";
        sphere_blue.GetComponent<Renderer>().material.SetColor("_Color", Color.blue);

        GameObject sphere_yellow = Instantiate(fixed_sphere, new Vector3(sphere_yellow_position.x, camera_height - sphereYGap, sphere_yellow_position.z), Quaternion.identity);
        sphere_yellow.name = "fixed_sphere_yellow";
        sphere_yellow.GetComponent<Renderer>().material.SetColor("_Color", Color.yellow);

        // Add instantiated spheres to calibration_spheres_list
        calibration_spheres_list.Add(sphere_red);
        calibration_spheres_list.Add(sphere_green);
        calibration_spheres_list.Add(sphere_blue);
        calibration_spheres_list.Add(sphere_yellow);

        // Log the addition of fixed calibration spheres
        Debug.Log("Fixed Calibration spheres added. Waiting for confirmation of position");
    }

    public void NewSphere()
    {
        // Get the main camera's transform
        Transform camera_transform = Camera.main.transform;

        // Get the camera's height
        float camera_height = camera_transform.position.y;

        // Calculate the position for the new sphere
        Vector3 sphere_1_position = camera_transform.position + camera_transform.forward * 1.5f;

        // Instantiate a new sphere and set its position
        GameObject sphere = Instantiate(calibration_sphere, new Vector3(sphere_1_position.x, camera_height - sphereYGap, sphere_1_position.z), Quaternion.identity);
        sphere.name = "sphere new";

        // Add the new sphere to the calibration_spheres_list
        calibration_spheres_list.Add(sphere);

        // Log the addition of the new sphere
        Debug.Log("New sphere added. Waiting for collecting GPS points");
    }

    private void startCalibrationGPS()
    {
        // Disable the clickedButton (assuming clickedButton is some UI element)
        clickedButton.IsEnabled = false;

        // Calculate square length based on slider value
        int square_length = 1 + Mathf.RoundToInt(10 * sliderSquareLength.SliderValue);
        Debug.Log("Square Length: " + square_length);
        calibrationText.SetText("Square Length: " + square_length);

        // Check if debugging indoor environment is toggled
        Debug.Log("Console: Debug Indoor " + checkBoxDebuggingIndoor.IsToggled.ToString());
        Debug.Log("Console: Debug Fake Coordinates " + checkBoxFakeCoords.IsToggled.ToString());
        useFakeCoordinates = checkBoxFakeCoords.IsToggled;

        // Get the main camera's transform
        Transform camera_transform = Camera.main.transform;
        float camera_height = camera_transform.position.y;

        // Calculate positions for calibration spheres
        Vector3 sphere_1_position = camera_transform.position + camera_transform.right * square_length / 2 + camera_transform.forward * square_length / 2;
        Vector3 sphere_2_position = camera_transform.position + camera_transform.right * square_length / 2 - camera_transform.forward * square_length / 2;
        Vector3 sphere_3_position = camera_transform.position - camera_transform.right * square_length / 2 + camera_transform.forward * square_length / 2;
        Vector3 sphere_4_position = camera_transform.position - camera_transform.right * square_length / 2 - camera_transform.forward * square_length / 2;

        // Instantiate and name calibration spheres
        GameObject sphere1 = Instantiate(calibration_sphere, new Vector3(sphere_1_position.x, camera_height - sphereYGap, sphere_1_position.z), Quaternion.identity);
        sphere1.name = "sphere 1";

        GameObject sphere2 = Instantiate(calibration_sphere, new Vector3(sphere_2_position.x, camera_height - sphereYGap, sphere_2_position.z), Quaternion.identity);
        sphere2.name = "sphere 2";

        GameObject sphere3 = Instantiate(calibration_sphere, new Vector3(sphere_3_position.x, camera_height - sphereYGap, sphere_3_position.z), Quaternion.identity);
        sphere3.name = "sphere 3";

        GameObject sphere4 = Instantiate(calibration_sphere, new Vector3(sphere_4_position.x, camera_height - sphereYGap, sphere_4_position.z), Quaternion.identity);
        sphere4.name = "sphere 4";

        // Add calibration spheres to the list
        calibration_spheres_list.Add(sphere1);
        calibration_spheres_list.Add(sphere2);
        calibration_spheres_list.Add(sphere3);
        calibration_spheres_list.Add(sphere4);

        // Log the addition of calibration spheres
        Debug.Log("Calibration spheres added. Waiting for collecting GPS points");

        // If debugging indoor environment is toggled on, set indoor transformation
        if (checkBoxDebuggingIndoor.IsToggled)
        {
            setTransformationIndoorDebugging();
        }
    }

    private void setTransformationFixedCalibration()
    {
        // Variables to store local geographic points of fixed spheres
        Vector3d localGeographicPointRed, localGeographicPointBlue, localGeographicPointGreen, localGeographicPointYellow;

        // Default CRS for fixed spheres
        string CRSFixedSpheres = "EPSG31256";

        // Log the selected fixed point set
        Debug.Log("Transformation for Fixed points set in: " + fixedPointSet.CurrentIndex);

        // Switch statement to set local geographic points based on the selected fixed point set
        switch (fixedPointSet.CurrentIndex)
        {
            // TU WIEN ROOF 1 WGS84
            case 0:
                CRSFixedSpheres = "WGS84";
                localGeographicPointRed = new Vector3d(16.36927804, 201.24, 48.19614101);
                localGeographicPointGreen = new Vector3d(16.369302912, 201.24, 48.19610911);
                localGeographicPointBlue = new Vector3d(16.3693508, 201.24, 48.19612636);
                localGeographicPointYellow = new Vector3d(16.36932478, 201.24, 48.19615868);
                break;
            // TU WIEN ROOF 1 EPSG31256
            case 1:
                CRSFixedSpheres = "EPSG31256";
                localGeographicPointRed = new Vector3d(2761.288291, 201.24, 339748.3182);
                localGeographicPointGreen = new Vector3d(2763.138717, 201.24, 339744.7719);
                localGeographicPointBlue = new Vector3d(2766.698147, 201.24, 339746.6916);
                localGeographicPointYellow = new Vector3d(2764.762206, 201.24, 339750.2845);
                break;
            // BARTEK ROUTE 1
            case 2:
                CRSFixedSpheres = "EPSG31256";
                localGeographicPointRed = new Vector3d(-2643.56635901559, 213.9, 333997.061855607);
                localGeographicPointGreen = new Vector3d(-2643.84794224892, 213.96, 333993.607954435);
                localGeographicPointBlue = new Vector3d(-2647.32195137519, 214.02, 333993.918580797);
                localGeographicPointYellow = new Vector3d(-2647.26719653011, 214.01, 333997.166241569);
                break;
            // FRONT GUSSHAUSS TU WIEN
            case 3:
                CRSFixedSpheres = "EPSG31256";
                localGeographicPointRed = new Vector3d(2895.77858728623, 173.911, 340013.787291902);
                localGeographicPointGreen = new Vector3d(2894.85611251859, 173.922, 340010.359851382);
                localGeographicPointBlue = new Vector3d(2891.2755104047, 173.93, 340011.321170782);
                localGeographicPointYellow = new Vector3d(2892.59718966408, 173.93, 340014.743232315);
                break;
            // Default to TU WIEN ROOF 1 WGS84 if index is out of range
            default:
                CRSFixedSpheres = "WGS84";
                localGeographicPointRed = new Vector3d(16.36927804, 201.24, 48.19614101);
                localGeographicPointGreen = new Vector3d(16.369302912, 201.24, 48.19610911);
                localGeographicPointBlue = new Vector3d(16.3693508, 201.24, 48.19612636);
                localGeographicPointYellow = new Vector3d(16.36932478, 201.24, 48.19615868);
                break;
        }

        // Convert fixed points from WGS84 to projected CRS if necessary
        if (CRSFixedSpheres.Equals("WGS84"))
        {
            double[] pointProjectedCRS;

            // Transform Red
            pointProjectedCRS = CRSTransformer.transformPoint("WGS84", projectedCRS, new double[] { localGeographicPointRed.x, localGeographicPointRed.z });
            localGeographicPointRed = new Vector3d(pointProjectedCRS[0], localGeographicPointRed.y, pointProjectedCRS[1]);

            // Transform Green
            pointProjectedCRS = CRSTransformer.transformPoint("WGS84", projectedCRS, new double[] { localGeographicPointGreen.x, localGeographicPointGreen.z });
            localGeographicPointGreen = new Vector3d(pointProjectedCRS[0], localGeographicPointGreen.y, pointProjectedCRS[1]);

            // Transform Blue
            pointProjectedCRS = CRSTransformer.transformPoint("WGS84", projectedCRS, new double[] { localGeographicPointBlue.x, localGeographicPointBlue.z });
            localGeographicPointBlue = new Vector3d(pointProjectedCRS[0], localGeographicPointBlue.y, pointProjectedCRS[1]);

            // Transform Yellow
            pointProjectedCRS = CRSTransformer.transformPoint("WGS84", projectedCRS, new double[] { localGeographicPointYellow.x, localGeographicPointYellow.z });
            localGeographicPointYellow = new Vector3d(pointProjectedCRS[0], localGeographicPointYellow.y, pointProjectedCRS[1]);

            CRSFixedSpheres = "EPSG31256";
        }

        // List to hold control points in WGS84 coordinates
        controlPointsWGS84 = new List<Vector3d>();

        // Add calibration spheres to corresponding lists
        foreach (GameObject calibration_sphere in calibration_spheres_list)
        {
            switch (calibration_sphere.name)
            {
                case "fixed_sphere_red":
                    controlPointsTargetUnity.Add(calibration_sphere.transform.position);
                    controlPointsSourceProjectedCRS.Add(localGeographicPointRed);
                    break;
                case "fixed_sphere_green":
                    controlPointsTargetUnity.Add(calibration_sphere.transform.position);
                    controlPointsSourceProjectedCRS.Add(localGeographicPointGreen);
                    break;
                case "fixed_sphere_blue":
                    controlPointsTargetUnity.Add(calibration_sphere.transform.position);
                    controlPointsSourceProjectedCRS.Add(localGeographicPointBlue);
                    break;
                case "fixed_sphere_yellow":
                    controlPointsTargetUnity.Add(calibration_sphere.transform.position);
                    controlPointsSourceProjectedCRS.Add(localGeographicPointYellow);
                    break;
            }
        }

        // Check if enough control points are available for transformation
        if (controlPointsSourceProjectedCRS.Count == 4 && controlPointsTargetUnity.Count == 4)
        {
            // Convert control points to WGS84
            foreach (Vector3d controlPointsSource in controlPointsSourceProjectedCRS) { 
                double[] pointProj = { controlPointsSource.x, controlPointsSource.y };
                double[] pointWGS84 = CRSTransformer.transformPoint(projectedCRS, "WGS84", pointProj);
                Debug.Log(string.Format("Control point WGS84: X(lon)={0}, Y(lat)={1}, Height(m)={2}   ", pointWGS84[0], pointWGS84[1], controlPointsSource.y));
                controlPointsWGS84.Add(new Vector3d(pointWGS84[0], controlPointsSource.y, pointWGS84[1]));
            }

            // Initialize Helmert Transformation
            InitializeHelmertTransformation();

            // Instantiate accuracy spheres
            instatiateteAccuracySpheres();

            // Deactivate calibration spheres
            foreach (GameObject sphere in calibration_spheres_list)
                sphere.SetActive(false);

        }
        else
        {
            // Log error if control points are missing
            Debug.Log("NOT ABLE TO SET TRANSFORMATION. Target or source points missing!");
        }

    }


    internal Vector3 getAntenaPosition()
    {
        // Check if antenna position is fixed or not
        if (checkBoxAntenaFixedPos.IsToggled)
        {
            // Log and return the antenna position
            Debug.Log("Antenna position: " + antena.transform.position);
            return antena.transform.position;
        }
        else
        {
            // Log and return the camera position if antenna position is not fixed
            Debug.Log("Camera position: " + Camera.main.transform.position);
            return Camera.main.transform.position;
        }
    }

    internal void setTransformation()
     {
        // Create a list of boolean values indicating if all points for each calibration sphere are collected
        List<bool> pointsCollected = calibration_spheres_list.Select(point => point.GetComponent<CalibrationSphereScript>().pointCollected).ToList();

        // Check if all points are collected, no accuracy spheres are instantiated, and all points are collected
        if (pointsCollected.Count == 4 && !accuracySpheresInstantiated && pointsCollected.All(x => x))
        {
            Debug.Log("Starting First transformation!");

            Debug.Log("Transformation collected GPS points WGS84 to " + projectedCRS);

            // Prepare control points for each calibration sphere
            foreach (GameObject calibration_sphere in calibration_spheres_list)
            {
                prepareControlPointsForThisSphere(calibration_sphere, projectedCRS);
            }

            // Generate fake source and target calibration points if useFakeCoordinates is true
            if (useFakeCoordinates)
                generateFakeSourceAndTargetCalibrationPoints();

            // Initialize Helmert Transformation
            InitializeHelmertTransformation();

            // Instantiate accuracy spheres
            instatiateteAccuracySpheres();
        }
        // Dynamic Calibration CASE: Check if more than 4 points are collected and accuracy spheres are instantiated
        else if (pointsCollected.Count > 4 && accuracySpheresInstantiated)
        {
            // Destroy existing accuracy spheres
            foreach (GameObject sphere in holo_sphere_list)
                Destroy(sphere);

            foreach (GameObject sphere in geo_sphere_list)
                Destroy(sphere);

            // Destroy any visualization
            vizualizationManager.GetComponent<VizualizationManager>().DestroyAll();

            // Clear lists and prepare control points for the last calibration sphere
            holo_sphere_list = new List<GameObject>();
            geo_sphere_list = new List<GameObject>();
        
            if (!useFakeCoordinates)
            {
                prepareControlPointsForThisSphere(calibration_spheres_list.Last(), projectedCRS);
            }
            else
            {
                // Generate fake coordinates
                Vector3 target = calibration_spheres_list.Last().GetComponent<CalibrationSphereScript>().localPoints.First().Value;
                var rand = new System.Random();
                double erroRatio = 40; // Maximum error
                controlPointsTargetUnity.Add(new Vector3(target.x + (float)(rand.NextDouble() / erroRatio), target.y + (float)(rand.NextDouble() / erroRatio), target.z + (float)(rand.NextDouble() / erroRatio)));
                controlPointsTargetUnity.Add(new Vector3(target.x + (float)(rand.NextDouble() / erroRatio), target.y + (float)(rand.NextDouble() / erroRatio), target.z + (float)(rand.NextDouble() / erroRatio)));
                controlPointsTargetUnity.Add(new Vector3(target.x + (float)(rand.NextDouble() / erroRatio), target.y + (float)(rand.NextDouble() / erroRatio), target.z + (float)(rand.NextDouble() / erroRatio)));

                Vector3d source = helmertTransformation.TransformLocal2Geo(target, true);
                Debug.Log("Transformed source for fake coordinate calibration on the fly: " + source.ToString2());
                erroRatio = 20;
                double deltaX = rand.NextDouble() / 10;
                double deltaY = rand.NextDouble() / 20;
                double deltaZ = rand.NextDouble() / 10;
                controlPointsSourceProjectedCRS.Add(new Vector3d(source.x + deltaX + (rand.NextDouble() / erroRatio), source.y + deltaY + (rand.NextDouble() / erroRatio), source.z + deltaZ + (rand.NextDouble() / erroRatio)));
                controlPointsSourceProjectedCRS.Add(new Vector3d(source.x + deltaX + (rand.NextDouble() / erroRatio), source.y + deltaY + (rand.NextDouble() / erroRatio), source.z + deltaZ + (rand.NextDouble() / erroRatio)));
                controlPointsSourceProjectedCRS.Add(new Vector3d(source.x + deltaX + (rand.NextDouble() / erroRatio), source.y + deltaY + (rand.NextDouble() / erroRatio), source.z + deltaZ + (rand.NextDouble() / erroRatio)));
            }

            // Reinitialize Helmert Transformation
            InitializeHelmertTransformation();

            // Instantiate accuracy spheres
            instatiateteAccuracySpheres();
        }
    }

    private void setTransformationSpatialAnchors()
    {
        Debug.Log("Starting First transformation using Spatial Anchor Spheres!");
        calibrationText.text = "Calculating Transformation from Geo_anchor Spheres";
        Debug.Log($"A total of {holo_sphere_list.Count} Geo_Anchor_Spheres were located");
        // Check if there are enough Geo_Anchor_Spheres for transformation
        if (holo_sphere_list.Count > 6)
        {
            // Prepare control points from Geo_Anchor_Spheres
            prepareControlPointsFromGeoAnchorSpheres(holo_sphere_list, projectedCRS);

            // Initialize Helmert Transformation
            InitializeHelmertTransformation();

            // Instantiate accuracy spheres for spatial anchor calibration
            instatiateteAccuracySpheresSpatialAnchorCalibration();
        }
        else
        {
            // Update UI text if not enough Geo_Anchor_Spheres are found
            calibrationText.text = $"Not enough Geo_Anchor_Spheres, only {holo_sphere_list.Count}";

            // Throw an exception if there are not enough Geo_Anchor_Spheres
            throw new Exception($"Not enough Geo_Anchor_Spheres, only {holo_sphere_list.Count}");
        }
    }



    private void setTransformationIndoorDebugging()
    {

        /****Generate fake source and target poinst for indoors calibration ****/
        generateFakeSourceAndTargetCalibrationPoints();

        InitializeHelmertTransformation();

        instatiateteAccuracySpheres();

    }

    /****Generate fake source and target poinst for indoors calibration ****/
    private void generateFakeSourceAndTargetCalibrationPoints()
    {
        System.Random rand = new System.Random();
        double erroRatio = 30; /* at most 1/erroRatio meters fake error */

        // Clear previous target calibration points
        controlPointsTargetUnity = new List<Vector3>();

        // Generate target calibration points for each sphere
        for (int i = 0; i < 4; i++)
        {
            float xOffset = (float)(rand.NextDouble() / erroRatio);
            float yOffset = (float)(rand.NextDouble() / erroRatio);
            float zOffset = (float)(rand.NextDouble() / erroRatio);

            // Sphere one
            controlPointsTargetUnity.Add(new Vector3(0.5f + xOffset, yOffset, 0.5f + zOffset));
            controlPointsTargetUnity.Add(new Vector3(0.5f + xOffset, yOffset, 0.5f + zOffset));
            controlPointsTargetUnity.Add(new Vector3(0.5f + xOffset, yOffset, 0.5f + zOffset));

            // Sphere two
            controlPointsTargetUnity.Add(new Vector3(0.5f + xOffset, yOffset, -0.5f + zOffset));
            controlPointsTargetUnity.Add(new Vector3(0.5f + xOffset, yOffset, -0.5f + zOffset));
            controlPointsTargetUnity.Add(new Vector3(0.5f + xOffset, yOffset, -0.5f + zOffset));

            // Sphere three
            controlPointsTargetUnity.Add(new Vector3(-0.5f + xOffset, yOffset, 0.5f + zOffset));
            controlPointsTargetUnity.Add(new Vector3(-0.5f + xOffset, yOffset, 0.5f + zOffset));
            controlPointsTargetUnity.Add(new Vector3(-0.5f + xOffset, yOffset, 0.5f + zOffset));

            // Sphere four
            controlPointsTargetUnity.Add(new Vector3(-0.5f + xOffset, yOffset, -0.5f + zOffset));
            controlPointsTargetUnity.Add(new Vector3(-0.5f + xOffset, yOffset, -0.5f + zOffset));
            controlPointsTargetUnity.Add(new Vector3(-0.5f + xOffset, yOffset, -0.5f + zOffset));
        }

        // Clear previous source calibration points
        controlPointsSourceProjectedCRS = new List<Vector3d>();

        // Set fake source calibration points based on selected CRS
        if (projectedCRS.Equals("EPSG31256"))
        {
            double x = 2752.89;
            double y = 339753.77;
            double calibrationHeight = 219.108 + 24.6 + 1.8;

            for (int i = 0; i < 4; i++)
            {
                double xOffset = rand.NextDouble() / erroRatio;
                double yOffset = rand.NextDouble() / erroRatio;
                double zOffset = rand.NextDouble() / erroRatio;

                // Sphere one
                controlPointsSourceProjectedCRS.Add(new Vector3d(x + xOffset, calibrationHeight, y + zOffset));
                controlPointsSourceProjectedCRS.Add(new Vector3d(x + xOffset, calibrationHeight, y + zOffset));
                controlPointsSourceProjectedCRS.Add(new Vector3d(x + xOffset, calibrationHeight, y + zOffset));

                // Sphere two
                controlPointsSourceProjectedCRS.Add(new Vector3d(x + xOffset, calibrationHeight, (y - 1) + zOffset));
                controlPointsSourceProjectedCRS.Add(new Vector3d(x + xOffset, calibrationHeight, (y - 1) + zOffset));
                controlPointsSourceProjectedCRS.Add(new Vector3d(x + xOffset, calibrationHeight, (y - 1) + zOffset));

                // Sphere three
                controlPointsSourceProjectedCRS.Add(new Vector3d((x - 1) + xOffset, calibrationHeight, y + zOffset));
                controlPointsSourceProjectedCRS.Add(new Vector3d((x - 1) + xOffset, calibrationHeight, y + zOffset));
                controlPointsSourceProjectedCRS.Add(new Vector3d((x - 1) + xOffset, calibrationHeight, y + zOffset));

                // Sphere four
                controlPointsSourceProjectedCRS.Add(new Vector3d((x - 1) + xOffset, calibrationHeight, (y - 1) + zOffset));
                controlPointsSourceProjectedCRS.Add(new Vector3d((x - 1) + xOffset, calibrationHeight, (y - 1) + zOffset));
                controlPointsSourceProjectedCRS.Add(new Vector3d((x - 1) + xOffset, calibrationHeight, (y - 1) + zOffset));
            }

            // Transform fake source points to WGS84 and add to controlPointsWGS84
            foreach (Vector3d controlPointsSource in controlPointsSourceProjectedCRS)
            {
                double[] pointProj = { controlPointsSource.x, controlPointsSource.y };
                double[] pointWGS84 = CRSTransformer.transformPoint(projectedCRS, "WGS84", pointProj);
                Debug.Log(string.Format("Control point WGS84: X(lon)={0}, Y(lat)={1}, Height(m)={2}", pointWGS84[0], pointWGS84[1], controlPointsSource.y));
                controlPointsWGS84.Add(new Vector3d(pointWGS84[0], controlPointsSource.y, pointWGS84[1]));
            }
        }
        else
        {
            // Throw an exception if no fake coordinates are set for the selected CRS
            throw new Exception("No fake coordinates set for selected CRS: " + projectedCRS);
        }
    }


    /****Find in this sphere the pair of control points
     Usuallay a calibration sphere has a least 3 local (GPS) points. 
    The function finds the best correspondent target (unity) points to each local point based on time approximation
    Convert the GPS points form WGS84 to a given projected CRS (UTM_33 or other) and add to Source points. 
    Find the local point colleted with minimal time diference from GPS and add to target points
    For each sphere is expetect 3 pair of control points. So source and target must at the end have a total of 12 points****/
    private void prepareControlPointsForThisSphere(GameObject calibration_sphere, string projectedCRS)
    {
        SortedList<System.DateTime, GPSPoint> gpsPoints = calibration_sphere.GetComponent<CalibrationSphereScript>().GPSPoints;
        Dictionary<System.DateTime, Vector3> localPoints = calibration_sphere.GetComponent<CalibrationSphereScript>().localPoints;

        Debug.Log(calibration_sphere.name + " has gps points:  " + gpsPoints.Count());
        Debug.Log(calibration_sphere.name + " has local points:  " + localPoints.Count());

        foreach (KeyValuePair<System.DateTime, GPSPoint> GPSPointKeyValue in gpsPoints)
        {
            // Extract GPS point data
            double longitude = GPSPointKeyValue.Value.longitude;
            double latitude = GPSPointKeyValue.Value.latitude;
            double height = GPSPointKeyValue.Value.height_m;

            // Log GPS point in WGS84
            Debug.Log($"GPS point WGS84: X(lon)={longitude}, Y(lat)={latitude}, Height(m)={height}");

            // Convert GPS points from WGS84 to the specified projected CRS
            double[] pointWGS84 = { longitude, latitude };
            double[] pointProjectedCRS = CRSTransformer.transformPoint("WGS84", projectedCRS, pointWGS84);

            // Log GPS point in the specified projected CRS
            Debug.Log($"GPS point {projectedCRS}: X={pointProjectedCRS[0]}, Y={pointProjectedCRS[1]}");

            // Add the projected GPS point to the source points
            controlPointsSourceProjectedCRS.Add(new Vector3d(pointProjectedCRS[0], height, pointProjectedCRS[1]));

            // Add the GPS point in WGS84 to controlPointsWGS84
            controlPointsWGS84.Add(new Vector3d(pointWGS84[0], height, pointWGS84[1]));

            // Find the local point collected with minimal time difference from GPS and add to target points
            Vector3 closestMatch = FindClosestLocalPointToGPS(GPSPointKeyValue.Key, localPoints);
            Debug.Log($"Unity closest point: X={closestMatch.x}, Y={closestMatch.y}, Z={closestMatch.z}");
            controlPointsTargetUnity.Add(closestMatch);
        }
    }
    /*Prepare control points using the Geoanchors loaded from ASA (see GeoSpatialAnchorManager.SpatialAnchorManager_AnchorLocated)*/
    private void prepareControlPointsFromGeoAnchorSpheres(List<GameObject> holo_sphere_list, string projectedCRS)
    {
        foreach (GameObject geoAnchorSphere in holo_sphere_list)
        {
            // Get the geo coordinates from the GeoAnchorScript attached to the sphere
            Vector3d geoCoord = geoAnchorSphere.GetComponent<GeoAnchorScript>().SourceGeoCoordinateWG84;

            // Extract longitude, latitude, and height
            double longitude = geoCoord.x;
            double latitude = geoCoord.z;
            double height = geoCoord.y;

            // Log GeoAnchor source in WGS84
            Debug.Log($"GeoAnchor source WGS84: X(lon)={longitude}, Y(lat)={latitude}, Height(m)={height}");

            // Convert GeoAnchor points from WGS84 to the specified projected CRS
            double[] pointWGS84 = { longitude, latitude };
            double[] pointProjectedCRS = CRSTransformer.transformPoint("WGS84", projectedCRS, pointWGS84);

            // Log GeoAnchor source in the specified projected CRS
            Debug.Log($"GeoAnchor source {projectedCRS}: X={pointProjectedCRS[0]}, Y={pointProjectedCRS[1]}");

            // Add the projected GeoAnchor point to the source points
            controlPointsSourceProjectedCRS.Add(new Vector3d(pointProjectedCRS[0], height, pointProjectedCRS[1]));

            // Add the GeoAnchor point in WGS84 to controlPointsWGS84
            controlPointsWGS84.Add(geoCoord);

            // Get the Unity position of the GeoAnchor sphere
            Vector3 geoAnchorPosition = geoAnchorSphere.transform.position;
        
            // Log Unity position
            Debug.Log($"Unity point: X={geoAnchorPosition.x}, Y={geoAnchorPosition.y}, Z={geoAnchorPosition.z}");

            // Add the Unity position to controlPointsTargetUnity
            controlPointsTargetUnity.Add(geoAnchorPosition);
        }
    }



    private static Vector3 FindClosestLocalPointToGPS(System.DateTime date, Dictionary<DateTime, Vector3> localPoints)
    {
        // Initialize variables to store the time difference and the best match time
        TimeSpan timeDiff = TimeSpan.MaxValue;
        DateTime bestMatch = DateTime.MaxValue;

        // Iterate through each local point
        foreach (KeyValuePair<System.DateTime, Vector3> localPointsKeyValue in localPoints)
        {
            // Calculate the time difference between the GPS point and the current local point
            TimeSpan currentDiff = (date - localPointsKeyValue.Key).Duration();

            // Update the best match if the current difference is smaller than the previous best match
            if (currentDiff < timeDiff)
            {
                timeDiff = currentDiff;
                bestMatch = localPointsKeyValue.Key;
            }
        }

        // Return the local point that corresponds to the best match time
        return localPoints[bestMatch];
    }


    private void InitializeHelmertTransformation()
    {
        // Display a message indicating the start of a new Helmert transformation
        calibrationText.text = "Starting New Helmert Transformation";

        // Instantiate the Helmert transformation object
        helmertTransformation = new SpatialTransformer.HelmertTransformation();

        // Log the total number of target points (Unity points) and source points (GPS points)
        Debug.Log("Total of Target points for Helmert transformation (Unity points): " + controlPointsTargetUnity.Count);
        Debug.Log("Total of Source points for Helmert transformation (GPS points): " + controlPointsSourceProjectedCRS.Count);

        // Prepare the transformation by assigning source and target points
        /*Assing the source and the target points for the transformation to _sourceGeographicObservationPoints and _targetLocalObservationPoints!         
       Since the distance from the source points are to small when comparing to their absolut values, the  A(trasnpose)*A matrix returns a singular matrix (not-invertible matrix).
       For this reason the origin of system is translated the mean of the source points.
       This means, that this translation must be reverted after transformation*/
        helmertTransformation.PrepareTransformation(controlPointsTargetUnity, controlPointsSourceProjectedCRS);


        // Compute an approximate solution for the transformation
        /*I uses the the first and the furhtest apart point to calculate the _rotationVector in a 2D similarity transformation (using 2 control point)
         * Thefore, it ignores the vertical coordinate! NOT SUITABLE FOR CALIBRATION SPHERE ON DIFFERENT HEIGHTS!
         * But why it needs this _rotation vector? To speed up the Least Square optimization*/
        helmertTransformation.ComputeApproximateSolution();

        // Compute the accuracy of the approximate solution
        helmertTransformation.ComputeAccuracy();
        Debug.Log("Approximate Hacc: " + helmertTransformation.HAcc + ", Vacc: " + helmertTransformation.VAcc);

        // Calculate parameters if approximate 
        helmertTransformation.CalculateParameters();
        // Recompute accuracy after calculating parameters
        /*Computer accuracy goes through all source points and make their transformation to target (unity). Then measu
        Later it takes the mean difference of the newly calculated target with the target in the control points*/
        helmertTransformation.ComputeAccuracy();
        Debug.Log("Final Hacc: " + helmertTransformation.HAcc + ", Vacc: " + helmertTransformation.VAcc);

        // Calculate the mean of all source points (geographic points) (WHY? Is this this shiffiting for more accurate transformation?
        meanSourcePoints = helmertTransformation.MeanCoordinate3D(controlPointsSourceProjectedCRS);

        // Display the accuracy of the transformation
        calibrationText.text = "Hacc: " + helmertTransformation.HAcc + ", Vacc: " + helmertTransformation.VAcc;
    }

    /**This method is used for the Dynamic calibration */
    private void UpdateHelmertTransformation()
    {
        // Display a message indicating the updating of the Helmert transformation
        calibrationText.text = "Updating Helmert Transformation";

        // Log the total number of target points (Unity points) and source points (GPS points)
        Debug.Log("Total of Target points for Helmert transformation (Unity points): " + controlPointsTargetUnity.Count);
        Debug.Log("Total of Source points for Helmert transformation (GPS points): " + controlPointsSourceProjectedCRS.Count);

        // Prepare the transformation by assigning source and target points
        helmertTransformation.PrepareTransformation(controlPointsTargetUnity, controlPointsSourceProjectedCRS);

        // Compute accuracy of the transformation
        helmertTransformation.ComputeAccuracy();
        Debug.Log("Current Hacc: " + helmertTransformation.HAcc + ", Vacc: " + helmertTransformation.VAcc);

        // Calculate parameters of the transformation
        helmertTransformation.CalculateParameters();
        // Recompute accuracy after calculating parameters
        helmertTransformation.ComputeAccuracy();
        Debug.Log("New Hacc: " + helmertTransformation.HAcc + ", Vacc: " + helmertTransformation.VAcc);

        // Calculate the mean of all source points
        meanSourcePoints = helmertTransformation.MeanCoordinate3D(controlPointsSourceProjectedCRS);

        // Display the accuracy of the transformation
        calibrationText.text = "Hacc: " + helmertTransformation.HAcc + ", Vacc: " + helmertTransformation.VAcc;
    }

    /****Compare distance of the Holo_spheres and the Geo_spheres to visualize the accuracy of the helmert transformation ****/
    /****Add spheres (holosphreres) using the local coordiates from unity, 
     * and add the spheres (geospheres) using projected coordinated converted to local using the hermert transformation.
     * Those sphere can be used to to visualize the accuracy of the transformation****/
    private void instatiateteAccuracySpheres()
    {
        // Iterate over all control points
        for (int i = 0; i < controlPointsTargetUnity.Count; i++)
        {
            // Instantiate Holo_spheres using target (unity camera) coordinates and add to the scene
            GameObject geoAnchorSphere = Instantiate(holo_sphere, controlPointsTargetUnity[i], Quaternion.identity);
            // Assign projected and WGS84 coordinates to the instantiated sphere
            geoAnchorSphere.GetComponent<GeoAnchorScript>().SourceGeoCoordinateProj = controlPointsSourceProjectedCRS[i];
            geoAnchorSphere.GetComponent<GeoAnchorScript>().SourceGeoCoordinateWG84 = controlPointsWGS84[i];
            // Set the color of the sphere to blue
            geoAnchorSphere.GetComponent<MeshRenderer>().material.color = Color.blue;
            // Add the instantiated sphere to the holo_sphere_list
            holo_sphere_list.Add(geoAnchorSphere);

            // Instantiate Geo_spheres using Projected coordinates converted to local using the Helmert transformation
            Vector3d temp = controlPointsSourceProjectedCRS[i] - meanSourcePoints;
            geo_sphere_list.Add(Instantiate(geo_sphere, helmertTransformation.TransformGeo2Local(temp.ToVector3()), Quaternion.identity));
        }

        // Set accuracySpheresInstantiated flag to true
        accuracySpheresInstantiated = true;
    }

    private void instatiateteAccuracySpheresSpatialAnchorCalibration()
    {
        // Set the color of all holo_spheres to blue
        foreach (GameObject sphere in holo_sphere_list)
            sphere.GetComponent<MeshRenderer>().material.color = Color.blue;

        // Iterate over all control points
        for (int i = 0; i < controlPointsTargetUnity.Count; i++)
        {
            // Instantiate Geo_spheres using Projected coordinates converted to local using the Helmert transformation
            Vector3d temp = controlPointsSourceProjectedCRS[i] - meanSourcePoints;
            geo_sphere_list.Add(Instantiate(geo_sphere, helmertTransformation.TransformGeo2Local(temp.ToVector3()), Quaternion.identity));
        }

        // Set accuracySpheresInstantiated flag to true
        accuracySpheresInstantiated = true;
    }

    public void toggleAccuracySpheres()
    {
        // Toggle the active state of each holo_sphere and geo_sphere
        foreach (GameObject sphere in holo_sphere_list)
            sphere.SetActive(!sphere.activeSelf);

        foreach (GameObject sphere in geo_sphere_list)
            sphere.SetActive(!sphere.activeSelf);
    }

    public void deactivateAccuracySpheres()
    {
        // Deactivate all holo_spheres and geo_spheres
        foreach (GameObject sphere in holo_sphere_list)
            sphere.SetActive(false);

        foreach (GameObject sphere in geo_sphere_list)
            sphere.SetActive(false);
    }



    public string getProjectedCRS()
    {
        return this.projectedCRS;
    }





}
