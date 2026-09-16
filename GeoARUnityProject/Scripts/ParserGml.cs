using System.Collections.Generic;
using UnityEngine;
using System;
using System.Xml;
using System.Globalization;

public class ParserGml
{


    internal static List<LandmarkColumn> LoadLandMarksColumnsFromResource(string fileName)
    {
        System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();

        Debug.Log(" Loading XML File " + fileName);

        TextAsset txtGMLAsset = Resources.Load<TextAsset>(fileName);
        //Debug.Log(" Loading XML File " + txtGMLAsset.name);
        //Debug.Log(txtGMLAsset.text);
        //Debug.Log(" create xml doc");
        xmlDoc.LoadXml(txtGMLAsset.text);
        Debug.Log(" xml loaded " + txtGMLAsset.name);

        return ReadLandMarkColumnsFromXMLDoc(xmlDoc, fileName);
    }



    
    private static List<LandmarkColumn> ReadLandMarkColumnsFromXMLDoc(XmlDocument xmlDoc, string fileName)
    {

        List<LandmarkColumn> landmarks = new List<LandmarkColumn>();

        // Why add name spaces: https://www.w3schools.com/xml/xml_namespaces.asp 
        var nsmgr = new System.Xml.XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("ogr", "http://ogr.maptools.org/");
        nsmgr.AddNamespace("gml", "http://www.opengis.net/gml/3.2");
        

        System.Xml.XmlNodeList featuresNodes = xmlDoc.SelectNodes("//ogr:FeatureCollection/ogr:featureMember/ogr:" + fileName, nsmgr);
        ///ogr:" + fileName + "/ogr:geometryProperty/gml:Point/gml:pos
        //Debug.Log("Total LM Features nodes found in xml: " + featuresNodes.Count);
        foreach (System.Xml.XmlNode featureNode in featuresNodes)
        {

            string coords = featureNode.SelectSingleNode("descendant::ogr:geometryProperty/gml:Point/gml:pos", nsmgr).InnerText;
            //Debug.Log(coords);
            string[] valueString = coords.Split(' ');
            //foreach (string s in valueString)
            //    Debug.Log(s);
            double[] valueDouble = StringArrayToDoubleArray(valueString);
            //foreach (double d in valueDouble)
             //   Debug.Log(d);

            LandmarkColumn landmark = new LandmarkColumn(valueDouble, "WGS84");

            try
            {
                string lmName = featureNode.SelectSingleNode("descendant::ogr:name", nsmgr).InnerText;
                Debug.Log("Reading Landmark: " + lmName);
                landmark.LmName = lmName;
            }
            catch (Exception e)
            {
                Debug.Log(e); //some node doesn't exists.}
            }


            try
            {
                string sideLength = featureNode.SelectSingleNode("descendant::ogr:side", nsmgr).InnerText;
                //Debug.Log(sideLength);
                landmark.SideLenght = float.Parse(sideLength, CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                Debug.Log(e); //some node doesn't exists.}
            }

            try
            {
                string heigth = featureNode.SelectSingleNode("descendant::ogr:height", nsmgr).InnerText;
                //Debug.Log(heigth);
                landmark.Height = float.Parse(heigth, CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                Debug.Log(e); //some node doesn't exists.}
            }

            try
            {
                string groundElevationAbs = featureNode.SelectSingleNode("descendant::ogr:groundabs", nsmgr).InnerText;
                //Debug.Log(groundElevationAbs);
                landmark.GroundHeight = float.Parse(groundElevationAbs, CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                Debug.Log(e); //some node doesn't exists.}
            }

            try
            {
                string groundElevationOffSet = featureNode.SelectSingleNode("descendant::ogr:groundoffs", nsmgr).InnerText;
                //Debug.Log(groundElevationOffSet);
                landmark.GroungHeightOffset = float.Parse(groundElevationOffSet, CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                Debug.Log(e); //some node doesn't exists.}
            }

            try
            {
                string color = featureNode.SelectSingleNode("descendant::ogr:color", nsmgr).InnerText;
                //Debug.Log(color);
                landmark.Color = color;
            }
            catch (Exception e)
            {
                Debug.Log(e); //some node doesn't exists.}
            }


            landmarks.Add(landmark);


        }

        return landmarks;

    }

    private static double[] StringArrayToDoubleArray(string[] sa)
    {
        double[] res;

        // Remove last element if null
        if (sa[sa.Length - 1] == "")
            res = new double[sa.Length - 1];
        else
            res = new double[sa.Length];

        for (int i = 0; i < sa.Length; i++)
        {
            try
            {

                //res[i] = float.Parse(sa[i].Replace('.', ','));
                res[i] = double.Parse(sa[i], CultureInfo.InvariantCulture);
            }
            catch
            {

            }
        }

        return res;
    }



}

