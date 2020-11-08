using System;
using System.Collections.Generic;

namespace FNNLib.Utilities {
    /// <summary>
    /// WIP: Observation manager.
    /// This handles the observation of network resources.
    ///
    /// This is added so that we can nicely handle the disconnection of users without causing slowdowns each time someone disconnects.
    /// Basically this means when observers are queried, we then process any removals.
    /// </summary>
    public class ObservationManager {
        private List<ulong> _observers = new List<ulong>();
        private uint _version;
        private static uint _globalVersion;

        /// <summary>
        /// Add an observer.
        /// </summary>
        /// <param name="observer"></param>
        public void Add(ulong observer) {
            if (NetworkManager.instance.connectedClients.ContainsKey(observer))
                _observers.Add(observer);
        }

        public void AddRange(List<ulong> observers) {
            observers.RemoveAll(client => !NetworkManager.instance.connectedClients.ContainsKey(client));
            _observers.AddRange(observers);
        }

        /// <summary>
        /// Remove an observer.
        /// </summary>
        /// <param name="observer"></param>
        public void Remove(ulong observer) {
            _observers.Remove(observer);
        }

        public bool IsObserving(ulong observer) {
            return _observers.Contains(observer);
        }

        /// <summary>
        /// Get all observers of this resource.
        /// </summary>
        /// <returns></returns>
        public List<ulong> GetObservers() {
            // If we're dirty, process global removals
            if (_version < _globalVersion) {
                _observers.RemoveAll(client => !NetworkManager.instance.connectedClients.ContainsKey(client));
                _version = _globalVersion;
            }
            
            // Return the observers
            return _observers;
        }

        /// <summary>
        /// Call this when someone connects or disconnects.
        /// This will mark the global state as dirty and the next time observer lists are queried, the disconnected clients will be removed.
        /// </summary>
        public static void MarkConnectionsDirty() {
            _globalVersion++;
        }
    }
}