using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;

[RequireComponent(typeof(XRGrabInteractable))]
public class VRDrillGrabbable : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference leftTriggerAction;
    [SerializeField] private InputActionReference rightTriggerAction;

    private XRGrabInteractable _grabInteractable;
    private DrillBit _drillBit;
    private bool _isHeld = false;

    private void Awake()
    {
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _drillBit = GetComponentInChildren<DrillBit>() ?? GetComponent<DrillBit>();

        _grabInteractable.selectEntered.AddListener(OnGrabbed);
        _grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnEnable()
    {
        leftTriggerAction?.action.Enable();
        rightTriggerAction?.action.Enable();
    }

    private void OnDisable()
    {
        leftTriggerAction?.action.Disable();
        rightTriggerAction?.action.Disable();
    }

    private void Update()
    {
        if (!_isHeld || _drillBit == null) return;

        float leftVal = leftTriggerAction?.action.ReadValue<float>() ?? 0f;
        float rightVal = rightTriggerAction?.action.ReadValue<float>() ?? 0f;
        bool triggerPressed = (leftVal > 0.1f) || (rightVal > 0.1f);

        _drillBit.SetDrilling(triggerPressed);
    }

    private void OnDestroy()
    {
        if (_grabInteractable == null) return;
        _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        _grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        _isHeld = true;
        if (_drillBit != null)
            _drillBit.isEquipped = true;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        _isHeld = false;
        if (_drillBit == null) return;
        _drillBit.isEquipped = false;
        _drillBit.SetDrilling(false);
    }


    // ── Editor helper ─────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void Reset()
    {
        if (GetComponent<XRGrabInteractable>() == null)
            gameObject.AddComponent<XRGrabInteractable>();
    }
#endif
}