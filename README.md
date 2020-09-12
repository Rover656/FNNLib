# Friday Night Networking Library

![Build Documentation](https://github.com/Rover656/FNNLib/workflows/Build%20Documentation/badge.svg) ![CI](https://github.com/Rover656/FNNLib/workflows/CI/badge.svg)

A **work in progress** networking solution for Unity, with a focus on extensibility and ease of use.

## Current features

- A basic client-server implementation with the ability to send and recieve packets through the basic TCP Transport
- A simple to understand and extensible transport system, allowing the integration of many networking pipelines.
- Networking capabilities/features can be extended beyond the offerings of the library with raw access to the packet system and serialization.

## Current goals

- Server's will support clients all on different scenes at different times. Good for party games.
- API will aim to be simple enough that no networking code has to be written, all you'll need are some NetworkBehaviours, checking if you're client or server (and of course a GUI of some form activating the NetworkManager).

## Possible future goals

- Completely rewritten TCP Transport (Currently we're using a variant of Mirror's Telepathy)
- Single player support (sounds silly, but adding the option for host mode without running a server listener is actually a good idea, takes th eload off devs that intend on developing a single and muliplayer game.)
