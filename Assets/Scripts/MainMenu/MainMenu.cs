using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public bool isStart;
    public bool isMap;
    public bool isQuit;
    void OnMouseUp(){
	if(isStart) {
		// todo
	}

    if(isMap) {
		// Only specifying the sceneName or sceneBuildIndex will load the Scene with the Single mode
        Debug.Log("Load:mapedit");
		SceneManager.LoadScene("mapedit", LoadSceneMode.Single);
    }
    
	if (isQuit) {
		Application.Quit();
	}
} 
}
