using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour {
    protected static T instance; 
    public static T Instance {
        get {
            if (instance == null) {
                instance = (T) FindObjectOfType(typeof(T));
                if (instance == null) {
                    Debug.LogError("An instance of " + typeof(T) + " is needed in the scene, but there is none.");
                }
            }
            return instance;
        }
    }
}