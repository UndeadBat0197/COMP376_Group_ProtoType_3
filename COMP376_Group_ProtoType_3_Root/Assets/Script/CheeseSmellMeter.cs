
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CheeseSmellMeter : MonoBehaviour
{
    public GameObject smellPanel;
    public Slider smellSlider;
    public TMP_Text smellText;

    private void Awake()
    {
        smellPanel.SetActive(false);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("SmellZone"))
            return;

        SphereCollider zone = other as SphereCollider;

        if (zone == null)
            return;

        // Find the smell zone's center and world-space radius
        Vector3 center = zone.transform.TransformPoint(zone.center);
        Vector3 scale = zone.transform.lossyScale;

        float radius = zone.radius * Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );

        float distance = Vector3.Distance(transform.position, center);

        // Smell is strongest at the center
        float strength = Mathf.Clamp01(1f - distance / radius);

        smellPanel.SetActive(true);
        smellSlider.value = strength;

        if (strength > 0.7f)
            smellText.text = "Strong smell!";
        else if (strength > 0.35f)
            smellText.text = "Getting closer...";
        else
            smellText.text = "Faint smell...";
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("SmellZone"))
            smellPanel.SetActive(false);
    }

    private void OnDisable()
    {
        if (smellPanel != null)
            smellPanel.SetActive(false);
    }
}
