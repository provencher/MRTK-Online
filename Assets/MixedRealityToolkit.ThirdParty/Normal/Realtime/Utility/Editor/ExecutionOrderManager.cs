using UnityEngine;
using UnityEditor;
using System;
using System.Collections;

namespace Normal.Utility {
    [InitializeOnLoad]
    public class ExecutionOrderManager {

        static ExecutionOrderManager() {
            ConfigureExecutionOrder();
        }

        static void ConfigureExecutionOrder() {
            foreach (MonoScript monoScript in MonoImporter.GetAllRuntimeMonoScripts()) {
                if (monoScript.GetClass() != null) {
                    foreach (ExecutionOrder attribute in Attribute.GetCustomAttributes(monoScript.GetClass(), typeof(ExecutionOrder))) {
                        var currentExecutionOrder = MonoImporter.GetExecutionOrder(monoScript);
                        var desiredExecutionOrder = attribute.order;
                        if (currentExecutionOrder != desiredExecutionOrder)
                            MonoImporter.SetExecutionOrder(monoScript, desiredExecutionOrder);
                    }
                }
            }
        }
    }
}
