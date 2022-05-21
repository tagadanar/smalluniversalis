using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private DateTime date = DateTime.Parse("0001/01/01");
    public Text textObject;

    public void Start()
    {
        textObject = GameObject.Find("DateText").GetComponent<Text>();
        textObject.text = date.ToString();
    }

    public void incrementDate()
    {
        date = date.AddDays(1);
        Debug.Log(date.ToString());
        textObject.text = date.ToString();
    }
}
