# Friday Night Networking Library

A *work in progress* networking solution for Unity, with a focus on extensibility and ease of use.

Current goals:

- Server's will support clients all on different scenes at different times. Good for party games.
- Server behaviours can be extended with access to the underlying packet system.
- API will aim to be simple enough that no networking code has to be written, all you'll need are some NetworkBehaviours, checking if you're client or server (and of course a GUI of some form activating the NetworkManager).

Possible future goals:

- Completely rewritten TCP Transport (Currently we're using a variant of Mirror's Telepathy)
- UDP Transport support
- WebSocket Transport
- Multiplex Transport
- Anything else really...
