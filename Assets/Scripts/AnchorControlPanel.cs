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

    public UnityEvent onRestoreAnchorsExecuted = new();
    public UnityEvent onClearAnchorsExecuted = new ();
    
    void Start()
    {
        restoreAllAnchorsButton.onClick.AddListener(() => onRestoreAnchorsExecuted.Invoke());
        clearAllAnchorsButton.onClick.AddListener(() => onClearAnchorsExecuted.Invoke());

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
