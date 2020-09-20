# Scene Management

FNNLib manages network scene sync for you by adding the `NetworkSceneManager` class. This should be used **instead** of Unity's own `SceneManager` as it deals with the network synchronization and spawning of objects.

## Loading scenes

You can load scenes either as a single scene, an [additive scene](https://docs.unity3d.com/ScriptReference/SceneManagement.LoadSceneMode.Additive.html) (Basically, where multiple scenes are loaded at once and overlap one another), or a scene that is additively loaded in the server, but is only a single scene on the client. The latter is managed using packing information, to spawn the scenes away from one another on the server so that the scenes don't interfere with one another. This is so that host mode does not render all scenes at once, as they will be offscreen until the host moves into them.

> Note: You must add the scene (and it's packing data if use use the third loading mode) to the NetworkManager's list of networked scenes before loading it, otherwise the scene will not load and an exception will be thrown.

You can load a single scene by simply calling:

```c#
NetworkSceneManager.LoadScene("sceneName");
```

To load a scene additively, instead use:

```c#
// Here, we state we want to load additively twice, the first is the server load mode and the second is the client load mode.
NetworkSceneManager.LoadScene("sceneName", LoadSceneMode.Additive, LoadSceneMode.Additive);
```

## Unloading scenes

You can unload a scene in the same way you would in Unity.

> Do note that a scene may only be unloaded if there are 2 or more loaded scenes.

```c#
var networkScene = ...; // You can get this either as a return value from LoadScene or with GetNetScene. GetSceneAt is to be implemented soon.
NetworkSceneManager.UnloadSceneAsync(networkScene);
```

## Active scenes

FNNLib does away with the concept of an active scene. Instead every client will have a list of loaded scenes.

## Moving clients

> To be documented soon.

## Moving GameObjects (Only those with Network Identities)

> To be documented soon.
