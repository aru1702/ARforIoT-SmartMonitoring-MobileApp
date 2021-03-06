﻿//================================================================================================================================
//
//  Copyright (c) 2015-2019 VisionStar Information Technology (Shanghai) Co., Ltd. All Rights Reserved.
//  EasyAR is the registered trademark or trademark of VisionStar Information Technology (Shanghai) Co., Ltd in China
//  and other countries for the augmented reality technology developed by VisionStar Information Technology (Shanghai) Co., Ltd.
//
//================================================================================================================================
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

using Models;
using Proyecto26;

using System;
using System.Threading.Tasks;

using easyar;
using System.Runtime.InteropServices;
public class ImageTargetController : MonoBehaviour
{

    public Button backButton, scanQrButton, retryButton;
    private int restartTime;
    private TextMesh device_name, sensor_details, device_lastupdate;
    private bool readyGetNewData;

    public enum TargetType
    {
        LocalImage,
        LocalTargetData,
        Cloud
    }

    [HideInInspector]
    public bool Tracked = false;
    public string TargetName = null;
    public string TargetPath = null;
    public float TargetSize = 1f;
    public PathType Type = PathType.StreamingAssets;
    public ImageTrackerBehaviour ImageTracker = null;

    private Target target = null;
    public TargetType targetType = TargetType.LocalImage;

    private bool xFlip = false;

    private easyar.Image targetImage;

    public Target Target()
    {
        return target;
    }
    public float TargetWidth
    {
        get
        {
            return transform.localScale.x;
        }
    }
    public float TargetHeight
    {
        get
        {
            return transform.localScale.y;
        }
    }

    public void SetTargetFromCloud(Target target)
    {
        this.target = target;
        targetType = TargetType.Cloud;
        var imageTarget = (target as ImageTarget);
        TargetSize = imageTarget.scale();
    }

    private void backButtonClick () {
        SceneManager.LoadScene(2);
        this.OnDestroy();
        Debug.Log("Back to Main menu");
    }

    private void scanQrButtonClick () {
        Debug.Log("Go to Scan QR");
        // var objects = GameObject.FindObjectsOfType(GameObject);
        // foreach (GameObject o in objects) {
        //     Destroy(o.gameObject);
        // }

        // await Task.Delay(TimeSpan.FromSeconds(0.5));
        // SceneManager.LoadScene(4);
        // await Task.Delay(TimeSpan.FromSeconds(0.5));
        // SceneManager.LoadScene(4);

        PlayerPrefs.SetInt("app__scene_number", 4);
        SceneManager.LoadScene(5);
    }

    private void retryButtonClick () {
        PlayerPrefs.SetString("app__go_time_record", "False");
        PlayerPrefs.SetString("app__end_time_record", "False");
        PlayerPrefs.SetString("app__time_record", "True");
        SceneManager.LoadScene(3);
        this.OnDestroy();
    }

    private void Start()
    {

// my code
        backButton.onClick.AddListener(backButtonClick);
        scanQrButton.onClick.AddListener(scanQrButtonClick);
        retryButton.onClick.AddListener(retryButtonClick);
        
        retryButton.interactable = false;
        restartTime = 50;
        
        device_name = GameObject.Find("device_name").GetComponent<TextMesh>();
        sensor_details = GameObject.Find("sensor_details").GetComponent<TextMesh>();
        device_lastupdate = GameObject.Find("device_lastupdate").GetComponent<TextMesh>();
        readyGetNewData = true;

// dev code
        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }
        switch (targetType)
        {
            case TargetType.LocalImage:
            case TargetType.LocalTargetData:
                StartCoroutine(LoadImageTarget());
                break;
            case TargetType.Cloud:
                if (target != null)
                {
                    TargetName = target.name();
                }
                break;
        }
    }

    private void OnEnable() {
        string appTimeRecord = PlayerPrefs.GetString("app__time_record");

        if (appTimeRecord == "True") {
            PlayerPrefs.SetString("app__end_time_record", "False");
            // PlayerPrefs.SetString("app__go_time_record", "False");
        } else {
            // PlayerPrefs.SetString("app__end_time_record", "True");
        }

        // Debug.Log(appTimeRecord);

        // string goTimeRecord = PlayerPrefs.GetString("app__go_time_record");
        // Debug.Log(goTimeRecord);
    }

    private IEnumerator LoadImageTarget()
    {
        var path = TargetPath;
        var type = Type;
        WWW www;
        if (type == PathType.Absolute)
        {
            path = easyar.Utility.AddFileHeader(path);
#if UNITY_ANDROID && !UNITY_EDITOR
            path = "file://" +  path;
#endif
        }
        else if (type == PathType.StreamingAssets)
        {
            path = easyar.Utility.AddFileHeader(Application.streamingAssetsPath + "/" + path);
        }
        Debug.Log("[EasyAR]:" + path);
        www = new WWW(path);
        while (!www.isDone)
        {
            yield return 0;
        }
        if (!string.IsNullOrEmpty(www.error))
        {
            Debug.LogError(www.error);
            www.Dispose();
            yield break;
        }
        var data = www.bytes;
        easyar.Buffer buffer = easyar.Buffer.create(data.Length);
        var ptr = buffer.data();
        Marshal.Copy(data, 0, ptr, data.Length);

        Optional<easyar.ImageTarget> op_target;
        if (targetType == TargetType.LocalImage)
        {
            var image = ImageHelper.decode(buffer);
            if (!image.OnSome)
            {
                throw new System.Exception("decode image file data failed");
            }

            var p = new ImageTargetParameters();
            p.setImage(image.Value);
            p.setName(TargetName);
            p.setScale(TargetSize);
            p.setUid("");
            p.setMeta("");
            op_target = ImageTarget.createFromParameters(p);

            if (!op_target.OnSome)
            {
                throw new System.Exception("create image target failed from image target parameters");
            }

            image.Value.Dispose();
            buffer.Dispose();
            p.Dispose();
        }
        else
        {
            op_target = ImageTarget.createFromTargetData(buffer);

            if (!op_target.OnSome)
            {
                throw new System.Exception("create image target failed from image target target data");
            }

            buffer.Dispose();
        }

        target = op_target.Value;
        Destroy(www.texture);
        www.Dispose();
        if (ImageTracker == null)
        {
            yield break;
        }
        ImageTracker.LoadImageTarget(this, (_target, status) =>
        {
            targetImage = ((_target as ImageTarget).images())[0];
            Debug.Log("[EasyAR] Targtet name: " + _target.name() + " target runtimeID: " + _target.runtimeID() + " load status: " + status);
            Debug.Log("[EasyAR] Target size" + targetImage.width() + " " + targetImage.height());
        });
    }

    private void Update()
    {
        if (target != null)
        {
            var target = this.target as ImageTarget;
        }
    }

    public void SetXFlip()
    {
        xFlip = !xFlip;
    }

    private void SetIoTData () {

        // string deviceId = "rR64KRSbb2Zn4fswlxk4";

        // try to get data from pref        
        string deviceId = PlayerPrefs.GetString("device__id");

        // check if device id is exists
        if (deviceId == null || deviceId == "") {
            device_name.text = "No device";
            device_lastupdate.text = "";
            sensor_details.text = "Please scan your device\nQR code first!";
            return;
        }

        // url for RestClient API
        string getDeviceDetailUrl = "https://myionic-c4817.firebaseapp.com/api/v1/Device/GetDevice/" + deviceId;
        string getAllSensorDataUrl = "https://myionic-c4817.firebaseapp.com/api/v1/Data/GetAll/" + deviceId;

        // get device details
        RestClient.Get(getDeviceDetailUrl).Then(res => {
            string result = res.Text;
            GetDeviceDetailsModel resultJson = JsonUtility.FromJson<GetDeviceDetailsModel>(result);

            if (resultJson.code == 200) {
                device_name.text = resultJson.result.name;
                device_lastupdate.text = resultJson.result.last_update;
            } else if (device_name.text.Length > 0 || device_lastupdate.text.Length > 0) {
                // do nothing
                // because data already set but the server maybe hang up
                // so don't display as an error
            } else {
                // literally error or no device found
                device_name.text = "No device name";
                device_lastupdate.text = "";
            }
        });

        // get all data
        RestClient.Get(getAllSensorDataUrl).Then(res => {
            string result = res.Text;
            GetAllDataModel resultJson = JsonUtility.FromJson<GetAllDataModel>(result);

            if (resultJson.code == 200) {   

                if (resultJson.result.Length > 0) {                    
                    string resultData = "";          

                    for (int i = 0 ; i < resultJson.result.Length ; i++) {
                        string sensorName = resultJson.result[i].name;
                        string sensorValue = resultJson.result[i].value;

                        resultData += sensorName + ": "	 + sensorValue + "\n";
                    }

                    sensor_details.text = resultData;
                } else {
                    sensor_details.text = "No sensor listed in this device";
                }
            } else if (resultJson.code == 404) {
                sensor_details.text = "No sensor listed in this device";
            } else if (sensor_details.text.Length > 0) {
                // do nothing
                // do not update the text just because error in the middle
            } else {
                sensor_details.text = "Error while getting data from server";
            }

            readyGetNewData = true;
        });
    }

    public void OnTracking(Matrix4x4 pose)
    {
        string appTimeRecord = PlayerPrefs.GetString("app__time_record");
        string goTimeRecord = PlayerPrefs.GetString("app__go_time_record");
        string endTimeRecord = PlayerPrefs.GetString("app__end_time_record");

        Debug.Log("APP: " + appTimeRecord + "\tGO: " + goTimeRecord + "\tEND: " + endTimeRecord);

// my code
            
        if (appTimeRecord == "True" && goTimeRecord == "True") {
            PlayerPrefs.SetString("app__go_time_record", "False");
            PlayerPrefs.SetString("app__end_time_record", "True");
            PlayerPrefs.SetString("app__time_record", "False");
            this.OnFound();
        }

        // if (appTimeRecord == "False" && goTimeRecord == "False") {
        //     Debug.Log("update end time record");
        //     PlayerPrefs.SetString("app__end_time_record", "True");
        // }

// dev code
        // if (goTimeRecord == "True" || endTimeRecord == "True") {
        if (appTimeRecord != "True") {
            // Debug.Log(readyGetNewData + " " + restartTime);

            if (readyGetNewData == true) {
                if (restartTime > 0) {
                    restartTime--;
                    // Debug.Log(restartTime);
                } else {
                    restartTime = 50;
                    readyGetNewData = false;
                    SetIoTData();
                }
            }

            Debug.Log("[EasyAR] OnTracking target name: " + target.name());
            easyar.Utility.SetMatrixOnTransform(transform, pose);
            if (xFlip)
            {
                var scale = transform.localScale;
                scale.x = -scale.x;
                transform.localScale = scale;
            }

            transform.localScale = transform.localScale * TargetSize;
        }
    }

    public void OnLost()
    {
// my code 
        device_name.text = "No device name";
        device_lastupdate.text = "";
        sensor_details.text = "No data";

// dev code
        Debug.Log("[EasyAR] OnLost targtet name: " + target.name());
        gameObject.SetActive(false);
        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }
    }

    public void OnFound()
    {
// my code 
        string appTimeRecord = PlayerPrefs.GetString("app__time_record");
        string goTimeRecord = PlayerPrefs.GetString("app__go_time_record");
        string endTimeRecord = PlayerPrefs.GetString("app__end_time_record");

        // if (goTimeRecord == "True" || endTimeRecord == "True") {
        if (appTimeRecord != "True") {
            device_name.text = "";
            sensor_details.text = "Please wait ...";

// dev code
            Debug.Log("[EasyAR] OnFound targtet name: " + target.name());
            gameObject.SetActive(true);
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(true);
            }
        }
    }

    private void OnDestroy()
    {
// my code
        PlayerPrefs.SetString("app__end_time_record", "False");
        PlayerPrefs.SetString("app__go_time_record", "False");

// dev code
        if (ImageTracker != null)
            ImageTracker.UnloadImageTarget(this, (target, status) => { Debug.Log("[EasyAR] Targtet name: " + target.name() + " Target runtimeID: " + target.runtimeID() + " load status: " + status); });
    }
}
