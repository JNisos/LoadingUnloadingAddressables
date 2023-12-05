using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;


 public class AssetReferenceRenderable : MonoBehaviour
 {
    [SerializeField]
    protected AssetReference _baseRenderable;
    
    [SerializeField]
    private bool _applyLayerToRoot = true;
    
    [SerializeField]
    private bool _applyLayerToChildren = true;
    
    [SerializeField]
    private bool _applyScale;
    
    [SerializeField]
    [Tooltip("Applies the scale to all the children of the AssetRenderable rather than on the AssetRenderable object itself.")]
    private bool _applyScaleToChildren;
    
    [SerializeField]
    private Vector3 _scale = Vector3.one;

    // [BoxGroup("Shader Proxies")]
    // [SerializeField]
    // private ShaderProxyDefinition[] _shaderProxies;

    private GameObject _cachedLoadedObject;
    
    private AsyncOperationHandle<GameObject> _instantiateAsyncOperationHandle;
    
    private static LocalKeyword _transparentLocalKeyword; 
    private static bool _transparentLocalKeywordSetup;

    public bool IsObjectLoaded => _cachedLoadedObject != null;
    
    private UnityEvent<Transform> _onRenderableLoaded { get; } = new ();

    private int _renderableCount;
    private Vector3 _tempReusedVector;
    private readonly Dictionary<Material, Material> _swappedMaterialsCache = new();
    
    
    protected virtual Transform GetParent()
    {
       return transform;
    }
    

    public void OnRenderableLoaded(UnityAction<Transform> callback, bool alwaysAddListener = false)
    {
       if (_instantiateAsyncOperationHandle.IsValid() &&
           _instantiateAsyncOperationHandle.IsDone &&
           _instantiateAsyncOperationHandle.Result != null &&
           IsObjectLoaded)
       {
          callback.Invoke(_instantiateAsyncOperationHandle.Result.transform);
          
          if (alwaysAddListener)
          {
             _onRenderableLoaded.AddListener(callback);
          }
       }
       else
       {
          _onRenderableLoaded.AddListener(callback);
       }
    }

    public void ClearOnRenderableLoadedListeners()
    {
       _onRenderableLoaded.RemoveAllListeners();
    }
    
    protected virtual void OnEnable()
    {
       if (_instantiateAsyncOperationHandle.IsValid()
           && _cachedLoadedObject != null
           && (_instantiateAsyncOperationHandle.IsDone || _instantiateAsyncOperationHandle.Result == null))
       {
          return;
       }

       var createRealtimeRenderable = Application.isPlaying;
       

       if (createRealtimeRenderable)
       {
          SleepParentRenderable();

          _instantiateAsyncOperationHandle = GetActiveAssetReference().InstantiateAsync(GetParent());
          if (_instantiateAsyncOperationHandle.IsDone)
          {
             OnLoadDone(_instantiateAsyncOperationHandle);
          }
          else
          {
             _instantiateAsyncOperationHandle.Completed += OnLoadDone;
          }
       }
    }

    private void SleepParentRenderable()
    {
       if (transform.parent == null)
       {
          return;
       }

       var rigidBody = transform.parent.GetComponent<Rigidbody>();
       if (rigidBody != null)
       {
          rigidBody.Sleep();
       }
    }

    private void OnDisable()
    {
       CleanUpAddressable();
    }


    private void CleanUpAddressable()
    {
       // Can't check _cachedLoadedObject == null because it might not have been assigned yet
       if (!_instantiateAsyncOperationHandle.IsValid())
       {
          return;
       }

       _instantiateAsyncOperationHandle.Completed -= OnLoadDone;
       Addressables.Release(_instantiateAsyncOperationHandle);
       _cachedLoadedObject = null;
    }

    private void OnDestroy()
    {
       if (_instantiateAsyncOperationHandle.IsValid())
       {
          _instantiateAsyncOperationHandle.Completed -= OnLoadDone;
       }
    }

    
    public void SetRenderable(AssetReference renderable)
    {
       _baseRenderable = renderable;
    }


    protected virtual AssetReference GetActiveAssetReference()
    {
       return _baseRenderable;
    }

    protected virtual void OnLoadDone(AsyncOperationHandle<GameObject> handle)
    {
       if (handle.Status == AsyncOperationStatus.Failed)
       {
          Debug.LogError("Failed to load {" + GetActiveAssetReference() + "} for {" + name + "}.");
          return;
       }

       if (gameObject == null || !gameObject.activeSelf || IsObjectLoaded || handle.Result == null)
       {
          CleanUpAddressable();
          return;
       }
       
       var objectTransform = handle.Result.transform;
       _cachedLoadedObject = objectTransform.gameObject;

       _swappedMaterialsCache.Clear();
       
       // ShaderProxyDefinition.ProcessShaderProxies(handle.Result, _shaderProxies, _swappedMaterialsCache);

       if (transform.parent != null && transform.parent.TryGetComponent(out Rigidbody rigidBody))
       {
          rigidBody.ResetCenterOfMass();
          rigidBody.ResetInertiaTensor();
          rigidBody.WakeUp();
       }
       
       ApplyLayers(objectTransform);

       
       if (GetParent() != transform)
       {
          ApplyScaleToChild(objectTransform);
       }
       
       ApplyTransformScaleSettings(objectTransform);
       ClearLocalPositionAndRotation(objectTransform);

       objectTransform.SetAsFirstSibling();

       handle.Completed -= OnLoadDone;
       _onRenderableLoaded?.Invoke(objectTransform);
    }

    private void ApplyLayers(Transform objectTransform)
    {
       if(_applyLayerToRoot)
       {
          objectTransform.tag = tag;
          objectTransform.gameObject.layer = gameObject.layer;
       }
       
       if(_applyLayerToChildren)
       {
          foreach (var child in objectTransform.GetComponentsInChildren<Transform>(true))
          {
             child.gameObject.layer = gameObject.layer;
          }
       }
    }
    
    private void ApplyTransformScaleSettings(Transform objectTransform)
    {
       if (!_applyScale)
       {
          return;
       }

       if (_applyScaleToChildren)
       {
          for (var i = 0; i < objectTransform.childCount; i++)
          {
             var child = objectTransform.GetChild(i);
             var currentScale = child.localScale;
             _tempReusedVector.x = currentScale.x * _scale.x;
             _tempReusedVector.y = currentScale.y * _scale.y;
             _tempReusedVector.z = currentScale.z * _scale.z;
             child.localScale = _tempReusedVector;
          }
       }
       else
       {
          var currentScale = objectTransform.localScale;
          _tempReusedVector.x = currentScale.x * _scale.x;
          _tempReusedVector.y = currentScale.y * _scale.y;
          _tempReusedVector.z = currentScale.z * _scale.z;
          objectTransform.localScale = _tempReusedVector;
       }
    }

    private static void ClearLocalPositionAndRotation(Transform objectTransform)
    {
       objectTransform.localPosition = Vector3.zero;
       objectTransform.localRotation = Quaternion.identity;
    }
    
    private void ApplyScaleToChild(Transform objectTransform)
    {
       var gameObjectTransform = gameObject.transform;
       var sourceScale = gameObjectTransform.localScale;
       var instanceScale = objectTransform.localScale;

       _tempReusedVector.x = instanceScale.x * sourceScale.x;
       _tempReusedVector.y = instanceScale.y * sourceScale.y;
       _tempReusedVector.z = instanceScale.z * sourceScale.z;
       objectTransform.localScale = _tempReusedVector;
    }

    
    public void SetApplyLayerToRoot(bool value) => _applyLayerToRoot = value;

    public void SetApplyLayerToChildren(bool value) => _applyLayerToChildren = value;
 }