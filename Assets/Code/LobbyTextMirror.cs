using TMPro;
using UnityEngine;

public class LobbyTextMirror : MonoBehaviour
{
    private TextMeshProUGUI source;
    private TextMeshProUGUI target;

    public void Initialize(TextMeshProUGUI sourceText, TextMeshProUGUI targetText)
    {
        source = sourceText;
        target = targetText;
    }

    private void Update()
    {
        if (source == null || target == null)
        {
            return;
        }

        target.text = source.text;
    }
}
