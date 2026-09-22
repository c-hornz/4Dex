using System.Collections.Generic;
using FourDex.World;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// Owns one <see cref="World4D"/> and the scene objects that make it up. An ordinary component, not a
    /// singleton: objects find it through the hierarchy or a serialized reference (SPEC §30, no hidden globals).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class World4DRegistry : MonoBehaviour
    {
        private readonly List<Object4DBehaviour> _members = new List<Object4DBehaviour>();
        private World4D? _world;

        // Lazy rather than in Awake, which does not run in edit mode.
        public World4D World => _world ??= new World4D();

        /// <summary>Registered objects, in registration order.</summary>
        public IReadOnlyList<Object4DBehaviour> Members => _members;

        /// <summary>Adds the object's authored 4D state to the world. Idempotent.</summary>
        public void Register(Object4DBehaviour member)
        {
            if (member.Body != null)
                return;
            Object4D body = World.Add(member.name, member.AuthoredTransform, member.AuthoredHalfExtents);
            member.Bind(this, body);
            _members.Add(member);
        }

        public void Unregister(Object4DBehaviour member)
        {
            if (member.Body == null || member.Registry != this)
                return;
            World.Remove(member.Body.Id);
            _members.Remove(member);
            member.Unbind();
        }
    }
}
