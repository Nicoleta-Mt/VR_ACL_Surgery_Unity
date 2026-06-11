using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using MarchingCubesProject;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class VoxelizeButton : MonoBehaviour
{
    [Header("References")]
    public PoseToVoxel poseToVoxel;

    [Header("Feedback")]
    public Renderer buttonRenderer;
    public Color normalColor = Color.white;
    public Color pressedColor = Color.green;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        interactable.selectEntered.AddListener(OnPressed);
    }

    private void OnPressed(SelectEnterEventArgs args)
    {
        poseToVoxel.ConfirmPoseAndVoxelize();
        StartCoroutine(FlashFeedback());
    }

    private System.Collections.IEnumerator FlashFeedback()
    {
        if (buttonRenderer != null)
        {
            buttonRenderer.material.color = pressedColor;
            yield return new WaitForSeconds(0.3f);
            buttonRenderer.material.color = normalColor;
        }
    }

    private void OnDestroy()
    {
        interactable.selectEntered.RemoveListener(OnPressed);
    }
}