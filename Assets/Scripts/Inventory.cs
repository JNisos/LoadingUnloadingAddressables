using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;

public class Inventory : MonoBehaviour
{
    [SerializeField] private GameObject inventory;
    [SerializeField] private List<AssetReference> references;
    private void Start()
    {
        foreach (var assetReference in references)
        {
            inventory.AddComponent<AssetReferenceRenderable>().SetRenderable(assetReference);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            inventory.SetActive(!inventory.activeInHierarchy);
        }
    }
}
