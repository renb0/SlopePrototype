using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleCameraFollow : MonoBehaviour {
    public Vector3 cameraOffset = new Vector3(0,0,-10);
    Camera cam;
    

    // Start is called before the first frame update
    void Start() {
        cam = Camera.main;
    }

    // Update is called once per frame
    void LateUpdate() {
        cam.transform.position = transform.position + cameraOffset;
    }
}
