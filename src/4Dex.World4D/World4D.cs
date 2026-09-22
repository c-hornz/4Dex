using System;
using System.Collections.Generic;
using FourDex.Math4D;

namespace FourDex.World
{
    /// <summary>
    /// The set of 4D objects in one world. An ordinary instance, not a singleton: the Unity side owns one
    /// through <c>World4DRegistry</c>, and tests make as many as they like (SPEC §30, no hidden global state).
    /// </summary>
    public sealed class World4D
    {
        private readonly Dictionary<int, Object4D> _objects = new Dictionary<int, Object4D>();
        private int _nextId = 1;

        public int Count => _objects.Count;

        public IEnumerable<Object4D> Objects => _objects.Values;

        public Object4D Add(string name, Transform4 transform, Vec4 halfExtents)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            var obj = new Object4D(_nextId, name, transform, halfExtents);
            _objects.Add(obj.Id, obj);
            _nextId++;
            return obj;
        }

        public bool Remove(int id) => _objects.Remove(id);

        public bool TryGet(int id, out Object4D obj)
        {
            if (_objects.TryGetValue(id, out Object4D? found))
            {
                obj = found;
                return true;
            }
            obj = null!;
            return false;
        }
    }
}
