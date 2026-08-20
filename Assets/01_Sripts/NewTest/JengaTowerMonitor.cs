using UnityEngine;

public class JengaTowerMonitor : MonoBehaviour
{
    private float checkTimer = 2.5f; // Tiempo de gracia al iniciar o reiniciar

    void Update()
    {
        if (JengaGameManager.Instance == null || JengaGameManager.Instance.isGameOver) return;

        if (checkTimer > 0)
        {
            checkTimer -= Time.deltaTime;
            return;
        }

        if (JengaGameManager.Instance.surfacePlane == null) return;

        float groundY = JengaGameManager.Instance.surfacePlane.position.y;
        JengaBlock[] blocks = Object.FindObjectsByType<JengaBlock>(FindObjectsSortMode.None);

        int fallenBlocksCount = 0;

        foreach (var block in blocks)
        {
            if (block != null)
            {
                // Condición 1: El bloque cayó por debajo del suelo o se alejó demasiado del centro
                bool isBelowGround = block.transform.position.y < groundY - 0.02f;

                // Condición 2: El bloque perdió su postura (está acostado o volcado, evaluando el vector Up local)
                bool isTippedOver = Vector3.Dot(block.transform.up, Vector3.up) < 0.5f;

                if (isBelowGround || isTippedOver)
                {
                    // Si el bloque fue tocado recientemente o ya cayó claramente
                    fallenBlocksCount++;
                }
            }
        }

        // Si hay 2 o más bloques caídos / volcados, o si el bloque que se estaba moviendo cayó
        if (fallenBlocksCount >= 2 || (JengaGameManager.Instance.currentMovedBlock != null &&
            (Vector3.Dot(JengaGameManager.Instance.currentMovedBlock.transform.up, Vector3.up) < 0.4f ||
             JengaGameManager.Instance.currentMovedBlock.transform.position.y < groundY - 0.02f)))
        {
            JengaGameManager.Instance.TriggerTowerCollapse("La torre se ha desestabilizado y colapsado");
            enabled = false;
        }
    }
}