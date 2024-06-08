using System.Collections;
using LearnXR.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class AnchorControlPanel : Singleton<AnchorControlPanel>
{
    [SerializeField] private Button restoreAllAnchorsButton;
    [SerializeField] private Button clearAllAnchorsButton;
    [SerializeField] private TextMeshProUGUI anchorsStatusText;
    [SerializeField] private float refreshAnchorsStatusFrequency = 0.5f;

    [SerializeField] private float distanceFromCamera = 0.5f;

    public UnityEvent onRestoreAnchorsExecuted = new();
    public UnityEvent onClearAnchorsExecuted = new ();

    private Camera mainCamera;
    public void ResetPosition()
    {
        // Calculate the new position
        Vector3 newPosition = mainCamera.transform.position + mainCamera.transform.forward * distanceFromCamera;

        // Set the object's position
        transform.position = newPosition;

        // Optional: Ensure the object faces the same direction as the camera
        transform.LookAt(mainCamera.transform);
        transform.transform.Rotate(0, 180, 0);
    }
    
    void Start()
    {
        restoreAllAnchorsButton.onClick.AddListener(() => onRestoreAnchorsExecuted.Invoke());
        clearAllAnchorsButton.onClick.AddListener(() => onClearAnchorsExecuted.Invoke());
        mainCamera = Camera.main;
        StartCoroutine(UpdateAnchorsStatus());
    }

    private IEnumerator UpdateAnchorsStatus()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshAnchorsStatusFrequency);
            anchorsStatusText.text = AnchorCreator.Instance.Status;
        }
    }

    private void OnDestroy()
    {
        onClearAnchorsExecuted.RemoveAllListeners();
    }
}
