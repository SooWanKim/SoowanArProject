using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ArManager : MonoBehaviour
{
    public Canvas canvas;
    public AssetReferenceGameObject joyStick;
    public AssetReferenceGameObject character;

    private GameObject _character;
    private GameObject _joyStick;
    private Joystick _joyStickComp;
    private List<ARRaycastHit> _raycastHits = new List<ARRaycastHit>();
    private ARRaycastManager _rayCastManager;
    private void Awake()
    {
        _rayCastManager = GetComponent<ARRaycastManager>();
    }

    void Start()
    {
        joyStick.LoadAssetAsync<GameObject>().Completed += (handle) =>
        {
            _joyStick = Instantiate(handle.Result);
            _joyStick.transform.parent = canvas.transform;
            _joyStick.transform.localScale = Vector3.one;
            _joyStickComp = _joyStick.GetComponent<Joystick>();
        };
    }

    void Update()
    {
        // if (Input.GetMouseButtonDown(0))
        // {
        //     Debug.Log("GetMouseButtonDown");
        //     if (_character == null)
        //     {
        //         character.LoadAssetAsync<GameObject>().Completed += (handle) =>
        //         {
        //             // _character = Instantiate(handle.Result, hitPose.position, hitPose.rotation);
        //             _character = Instantiate(handle.Result);
        //             PlayerInput input = _character.GetComponent<PlayerInput>();
        //             input.Joytick = _joyStickComp;
        //         };
        //     }
        // }

        for (int i = 0; i < Input.touchCount; ++i)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                if (_rayCastManager.Raycast(touch.position, _raycastHits, TrackableType.PlaneWithinPolygon))
                {
                    Pose hitPose = _raycastHits[0].pose;
                    if (_character == null)
                    {
                        character.LoadAssetAsync<GameObject>().Completed += (handle) =>
                        {
                            // _character = Instantiate(handle.Result, hitPose.position, hitPose.rotation);
                            _character = Instantiate(handle.Result, hitPose.position, hitPose.rotation);
                            PlayerInput input = _character.GetComponent<PlayerInput>();
                            input.Joytick = _joyStickComp;
                        };
                    }
                    else
                    {
                        _character.GetComponent<CharacterController>().enabled = false;
                        _character.transform.position = hitPose.position;
                        _character.GetComponent<CharacterController>().enabled = true;
                    }
                }
            }
        }
    }
}

