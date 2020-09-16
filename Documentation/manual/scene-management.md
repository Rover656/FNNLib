# Scene Management

FNNLib manages network scene sync for you by adding the `NetworkSceneManager` class. This should be used **instead** of Unity's own `SceneManager` as it deals with the network synchronization and spawning of objects.

## Loading scenes

You can load scenes either as a single scene, an additive scene, or a scene that is additively loaded in the server, but is a single scene on the client. Single and additive scenes work exactly the same as in Unity's own manager. However the third case is managed using packing information, to spawn the scenes away from one another on the server so that the scenes don't interfere with one another. This is so that host mode does not render all scenes at once, as they will be offscreen until the host moves into them.

You can load a single scene by simply calling:

```c#
NetworkSceneManager.LoadScene("sceneName");
```

To load a scene additively, instead use:

```c#
NetworkSceneManager.LoadScene("sceneName", LoadSceneMode.Additive);
```

## Unloading scenes

You can unload a scene in the same way you would in Unity also:

```c#
var networkScene = ...; // You can get this either as a return value from LoadScene, or GetActiveScene. GetSceneAt is to be implemented soon.
NetworkSceneManager.UnloadSceneAsync(networkScene);
```

## Active scenes

You can mark a scene as active too! Doing this means that any new clients will move to this scene and using Object.Instantiate would create objects in that scene. However, more on Object.Instantiate in the next section. The active scene can be queried and set with:

```c#
// Get
var activeScene = NetworkSceneManager.GetActiveScene();

// Set
NetworkSceneManager.SetActiveScene(someOtherScene); // or use its network ID
```

## Object.Instantiate, FindObject(s)OfType etc.

Because FNNLib supports loading of multiple scenes, this causes some problems with Unity's built in Instantiate and FindObjectsOfType methods. To fix this, the NetworkScene class implements helper functions for these. It is recommended that you use these all the time, and avoid use of the Unity versions to prevent confusion.

> Do note that using FindObjectsOfType will be obscenely slow, and it is recommended to use alternatives at all costs before using it. If you do use it, refrain from doing so every frame.

```c#
var someScene = ...;
someScene.Instantiate(myPrefab, Vector3.zero, Quaternion.identity);

var objects = someScene.FindObjectsOfType<MyBehaviour>();
```

## Moving clients

You can move clients to different scenes (if you have multiple scenes loaded additively) using the following:

```c#
NetworkSceneManager.MoveClientToScene(clientID, networkScene);
// or
NetworkSceneManager.MoveClientToScene(clientID, sceneNetworkID);
```

## Moving GameObjects (Only those with Network Identities)

This is yet to be implemented, but it will be simple to do so.
