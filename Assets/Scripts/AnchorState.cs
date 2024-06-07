using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class AnchorState : MonoBehaviour
{
    private TextMeshPro anchorText;
    private ARAnchor anchor;
    private Camera mainCamera;
    
    private void Start()
    {
        anchor = GetComponent<ARAnchor>();
        anchorText = GetComponentInChildren<TextMeshPro>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        anchorText.text = $"{anchor.gameObject.transform.position} - {anchor.trackingState}";
        anchorText.transform.LookAt(mainCamera.transform);
        anchorText.transform.Rotate(0, 180, 0);
    }
}