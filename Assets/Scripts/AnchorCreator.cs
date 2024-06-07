using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LearnXR.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.MagicLeapSupport;
using UnityEngine.XR.OpenXR.NativeTypes;
using Logger = LearnXR.Core.Logger;

public class AnchorCreator : Singleton<AnchorCreator>
{
    // anchor creation options  
    [SerializeField] private GameObject anchorPrefab;
    [SerializeField] private Transform controllerTransform;
    [SerializeField] private InputActionProperty bumperInputAction;
    [SerializeField] private float queryAnchorRadius = 10.0f;
    
    // active subsystem used for querying anchor confidence
    public MLXrAnchorSubsystem ActiveSubsystem { get; private set; }
    
    // anchor storage area
    private MagicLeapSpatialAnchorsStorageFeature storage;
    
    private struct StoredAnchor
    {
        public ulong AnchorId;
        public string AnchorMapPositionId;
        public ARAnchor AnchorObject;
    }
    
    private List<ARAnchor> localAnchors = new ();
    private List<StoredAnchor> storedAnchors =  new();

    public string Status => $"Local: {localAnchors.Count} Stored: {storedAnchors.Count}";

    private GameObject lastCreatedObjectForAnchor;
    
    private IEnumerator Start()
    {
        yield return new WaitUntil(IsMagicLeapAnchorSubsystemsLoaded);
        Logger.Instance.LogInfo("Magic Leap Subsystem Loaded");
        
        bumperInputAction.action.Enable();
        
        bumperInputAction.action.canceled += OnBumperActionReleased;
        
        AttachStorageListeners();
        
        // listen to anchor control panel actions
        AnchorControlPanel.Instance.onRestoreAnchorsExecuted.AddListener(QueryAnchors);
        AnchorControlPanel.Instance.onClearAnchorsExecuted.AddListener(ClearAllAnchors);
    }

    private void Update()
    {
        if (bumperInputAction.action.IsPressed())
        {
            Logger.Instance.LogInfo("Bumper Pressed");
            if (lastCreatedObjectForAnchor == null)
            {
                lastCreatedObjectForAnchor = Instantiate(anchorPrefab, controllerTransform.position,
                    controllerTransform.rotation);
                lastCreatedObjectForAnchor.transform.GetChild(0).GetComponent<Renderer>()
                    .material.color = Color.gray;
            }
            else
                lastCreatedObjectForAnchor.transform.SetPositionAndRotation(controllerTransform.position, controllerTransform.rotation);
        }
    }

    // Bumper started and canceled actions
    private void OnBumperActionReleased(InputAction.CallbackContext _)
    {
        Logger.Instance.LogInfo("Bumper Released");
        if(lastCreatedObjectForAnchor != null) CreateAnchor(persist: true);
    }

    private void ClearAllAnchors()
    {
        // clear local anchors
        if (localAnchors.Count > 0)
        {
            Logger.Instance.LogInfo($"Clearing local anchors count {localAnchors.Count}");
            for (int i = localAnchors.Count - 1; i >= 0; i--)
            {
                Destroy(localAnchors[i].gameObject);
                localAnchors.RemoveAt(i);
            }
        }
        
        // clear stored anchors
        if (storedAnchors.Count > 0)
        {
            Logger.Instance.LogInfo($"Clearing stored anchors count {storedAnchors.Count}");
            for (int i = storedAnchors.Count - 1; i >= 0; i--)
            {
                storage.DeleteStoredSpatialAnchor(new List<string> { storedAnchors[i].AnchorMapPositionId });
            }
        }
    }

    // Internal Anchor CRUD Methods
    private void CreateAnchor(bool persist = false)
    {
        lastCreatedObjectForAnchor.transform.GetChild(0).GetComponent<Renderer>()
            .material.color = Color.red;
        
        ARAnchor newAnchor = lastCreatedObjectForAnchor.AddComponent<ARAnchor>();
        lastCreatedObjectForAnchor.AddComponent<AnchorState>();
        
        localAnchors.Add(newAnchor);
        lastCreatedObjectForAnchor = null;
        
        if (persist)
        {
            StartCoroutine(PublishAnchor(newAnchor));
        }
        Logger.Instance.LogInfo($"New anchor created | Persistence: {persist}");
        
    }
    
    // Communication with Anchor Storage METHODS
    private void QueryAnchors()
    {
        if (!storage.QueryStoredSpatialAnchors(controllerTransform.transform.position, queryAnchorRadius))
        {
            Logger.Instance.LogError("There was a problem querying stored anchors");
        }
        else
        {
            Logger.Instance.LogInfo($"Querying Anchors within a radius of {queryAnchorRadius}");
        }
    }
    
    private IEnumerator PublishAnchor(ARAnchor toPublish)
    {
        while (toPublish.trackingState != TrackingState.Tracking)
            yield return null;

        storage.PublishSpatialAnchorsToStorage(new List<ARAnchor> {toPublish}, 0);            
    }
    
    private void AttachStorageListeners()
    {
        storage = OpenXRSettings.Instance.GetFeature<MagicLeapSpatialAnchorsStorageFeature>();
        
        // Querying Storage for a list of publish Anchors
        storage.OnQueryComplete += OnQueryCompleted;
        
        // Anchors Created from a list of Map Location Ids from Querying Storage
        storage.OnCreationCompleteFromStorage += OnCompletedCreation;
        
        // Publishing Local Anchor to Storage
        storage.OnPublishComplete += OnPublishCompleted;
        
        // Deleting a published Anchor from Storage
        storage.OnDeletedComplete += OnDeleteComplete;
    }

    private void OnQueryCompleted(List<string> anchorMapPositionIds)
    {
        foreach (var anchorMapPositionId in anchorMapPositionIds)
        {
            Logger.Instance.LogInfo($"OnQueryCompleted anchorMapPositionId: {anchorMapPositionId}");

            var foundStoredAnchorMatch = storedAnchors.Where(a => a.AnchorMapPositionId == anchorMapPositionId);
            if (!foundStoredAnchorMatch.Any())
            {
                Logger.Instance.LogInfo($"Creating {anchorMapPositionId} from storage");
                if (!storage.CreateSpatialAnchorsFromStorage(new List<string>() { anchorMapPositionId }))
                {
                    Logger.Instance.LogError(
                        $"Couldn't create spatial anchorMapPositionId: {anchorMapPositionId} from storage");
                }
            }
        }
    }
    
    private void OnCompletedCreation(Pose pose, ulong anchorId, 
        string anchorMapPositionId, XrResult result)
    {
        if (result != XrResult.Success)
        {
            Logger.Instance.LogError($"OnAnchorCompletedCreationFromStorage results: {result}");
            return;
        }

        StoredAnchor newStoredAnchor;
        newStoredAnchor.AnchorId = anchorId;
        newStoredAnchor.AnchorMapPositionId = anchorMapPositionId;

        GameObject newAnchor = Instantiate(anchorPrefab, pose.position, pose.rotation);
        ARAnchor newAnchorComponent = newAnchor.AddComponent<ARAnchor>();
        newAnchor.AddComponent<AnchorState>();
        
        newStoredAnchor.AnchorObject = newAnchorComponent;
        storedAnchors.Add(newStoredAnchor);
    }
    
    private void OnPublishCompleted(ulong anchorId, string anchorMapPositionId)
    {
        Logger.Instance.LogInfo($"OnPublishComplete AnchorId: {anchorId} AnchorMapPositionId: {anchorMapPositionId}");

        for (int i = localAnchors.Count - 1; i >= 0; i--)
        {
            if (ActiveSubsystem.GetAnchorId(localAnchors[i]) == anchorId)
            {
                StoredAnchor newsStoredAnchor;
                newsStoredAnchor.AnchorId = anchorId;
                newsStoredAnchor.AnchorMapPositionId = anchorMapPositionId;
                newsStoredAnchor.AnchorObject = localAnchors[i];

                storedAnchors.Add(newsStoredAnchor);
                localAnchors.RemoveAt(i);
                break;
            }
        }
    }
    
    private void OnDeleteComplete(List<string> anchorMapPositionIds)
    {
        foreach (var anchorMapPositionId in anchorMapPositionIds)
        {
            var storedAnchorIndex = storedAnchors.FindIndex(a => a.AnchorMapPositionId == anchorMapPositionId);
            if (storedAnchorIndex >= 0) // found
            {
                Destroy(storedAnchors[storedAnchorIndex].AnchorObject.gameObject);
                storedAnchors.RemoveAt(storedAnchorIndex);
                Logger.Instance.LogInfo($"AnchorId: {storedAnchors[storedAnchorIndex].AnchorId} " +
                                        $"AnchorMapPositionId: {storedAnchors[storedAnchorIndex].AnchorMapPositionId} deleted from storage:");
            }
        }
    }
    
    private bool IsMagicLeapAnchorSubsystemsLoaded()
    {
        if (XRGeneralSettings.Instance == null || XRGeneralSettings.Instance.Manager == null || XRGeneralSettings.Instance.Manager.activeLoader == null) return false;
        ActiveSubsystem = XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRAnchorSubsystem>() as MLXrAnchorSubsystem;
        return ActiveSubsystem != null;
    }
}