using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR.Features.MagicLeapSupport;

public class AnchorState : MonoBehaviour
{
    private TextMeshPro anchorText;
    private ARAnchor anchor;
    private Camera mainCamera;
    private MLXrAnchorSubsystem activeSubsystem;
    
    private void Start()
    {
        anchor = GetComponent<ARAnchor>();
        anchorText = GetComponentInChildren<TextMeshPro>();
        mainCamera = Camera.main;
        activeSubsystem = AnchorCreator.Instance.ActiveSubsystem;
    }

    private void Update()
    {
        if (activeSubsystem != null)
        {
            ulong magicLeapAnchorId = activeSubsystem.GetAnchorId(anchor);
            MLXrAnchorSubsystem.AnchorConfidence confidence = activeSubsystem.GetAnchorConfidence(anchor);
            anchorText.text =
                $"<color=green>AnchorId: {magicLeapAnchorId}</color>\n" +
                $"Confidence: {confidence}\n" +
                $"Pos: {anchor.gameObject.transform.position}\n" +
                $"Rot: {anchor.gameObject.transform.rotation}\n" +
                $"State: {anchor.trackingState}";
            anchorText.transform.LookAt(mainCamera.transform);
            anchorText.transform.Rotate(0, 180, 0);
        }
    }
}