using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OculusRefreshController : MonoBehaviour
{
#if OCULUSINTEGRATION_PRESENT
    void Update()
    {
        float maxFrequencyAvailable = 60f;
        foreach (var fq in OVRPlugin.systemDisplayFrequenciesAvailable)
        {
            maxFrequencyAvailable = Mathf.Max(fq, maxFrequencyAvailable);
        }
        OVRPlugin.systemDisplayFrequency = maxFrequencyAvailable;

        // Set physics settings
        float physicsTimestep = 1 / maxFrequencyAvailable;
        Time.fixedDeltaTime = physicsTimestep;
        Application.targetFrameRate = (int)(maxFrequencyAvailable + 0.01f);
        QualitySettings.vSyncCount = 1;

        enabled = false;
    }
#endif
}