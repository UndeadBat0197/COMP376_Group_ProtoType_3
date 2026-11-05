
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
        HideSmell();
    }

    private void OnEnable()
    {
        HideSmell();
    }

    private void OnTriggerEnter(Collider other)
    {
        UpdateSmell(other);
    }

    private void OnTriggerStay(Collider other)
    {
        UpdateSmell(other);
    }

    private void UpdateSmell(Collider other)
    {
        if (!other.CompareTag("SmellZone"))
            return;

        SphereCollider zone = other as SphereCollider;
        if (zone == null)
            return;

        if (smellPanel == null ||
            smellSlider == null ||
            smellText == null)
            return;

        Vector3 center =
            zone.transform.TransformPoint(zone.center);

        Vector3 scale = zone.transform.lossyScale;

        float radius = zone.radius * Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );

        if (radius <= 0f)
            return;

        float distance =
            Vector3.Distance(transform.position, center);

        float strength =
            Mathf.Clamp01(1f - distance / radius);

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
            HideSmell();
    }

    private void OnDisable()
    {
        HideSmell();
    }

    private void HideSmell()
    {
        if (smellPanel != null)
            smellPanel.SetActive(false);
    }
}
