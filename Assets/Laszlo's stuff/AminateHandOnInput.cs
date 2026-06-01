using UnityEngine;
using UnityEngine.InputSystem;

public class AminateHandOnInput : MonoBehaviour

{
    public InputActionProperty triggerValue;
    public InputActionProperty gripValue;

    public Animator handAnimator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (triggerValue != null && triggerValue.action != null)
        {
            float trigger = triggerValue.action.ReadValue<float>();
            if (handAnimator != null) handAnimator.SetFloat("Trigger", trigger);
        }

        if (gripValue != null && gripValue.action != null)
        {
            float grip = gripValue.action.ReadValue<float>();
            if (handAnimator != null) handAnimator.SetFloat("Grip", grip);
        }
    }
}
