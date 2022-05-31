using UnityEngine;
using System.Collections;

public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    public virtual void Awake()
    {
        _instance = GetComponent<T>();
    }

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = GameObject.FindObjectOfType<T>();
                if (_instance == null)
                {
                    GameObject gObj = new GameObject();
                    _instance = gObj.AddComponent<T>();
                }
            }

            return _instance;
        }
    }
}

public class SingletonNoMono<T> where T : class, new()
{
    private static T instance;
    public SingletonNoMono() { }
    public static T Instance
    {
        get
        {
            if (instance == null)
                instance = new T();

            return instance;
        }
    }
}

