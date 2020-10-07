using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public bool isStart;
    public bool isMap;
    public bool isQuit;
    void OnMouseUp(){
	if(isStart) {
		// Application.LoadLevel(1);
	}

    if(isMap) {
		GetComponent<Renderer>().material.color=Color.cyan;
    }
    
	if (isQuit) {
		Application.Quit();
	}
} 
}
