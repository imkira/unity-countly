using UnityEngine;
using System.Collections.Generic;

public class UnityCountlyDemo : MonoBehaviour
{
  private void Awake()
  {
    CountlyManager.Init("put_your_app_key_here");
  }

  public void EmitPurchase()
  {
    double price = 100;
    CountlyManager.Emit("purchase", 1, price,
      new Dictionary<string, string>()
      {
        {"purchase_id", "product01"},
      });
  }

  public void EmitCrazyEvent()
  {
    CountlyManager.Emit("UTF8こんにちはWorld", 1, 10.25,
      new Dictionary<string, string>()
      {
        {"demo1", "demo2"},
        {"demo3", "Handles UTF8-テスト JSON\"\nstrings"},
        {"demo4", "1"}
      });
  }

  private void OnGUI()
  {
    Rect rect;

    rect = new Rect(10, Screen.height - 320, Screen.width - 20, 150);

    if (GUI.Button(rect, "Emit purchase event"))
    {
      Debug.Log("Emitting purchase event...");

      EmitPurchase();
    }

    rect = new Rect(10, Screen.height - 160, Screen.width - 20, 150);

    if (GUI.Button(rect, "Emit crazy event"))
    {
      Debug.Log("Emitting crazy event...");

      EmitCrazyEvent();
    }
  }
}
