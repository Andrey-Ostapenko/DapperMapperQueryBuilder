using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Dynamic;
using MQBStatic;
using System.Collections;

namespace Mapper
{
    /// <summary>
    /// Use this to get DapperMapper objects.
    /// </summary>
    public class MapperStore : DMStatic_Store
    {
        /// <summary>
        /// Store t as a type that can be, and have been configurated for, mapped by DapperMapper. If you are configurating a type, use 
        /// MapperConfig.EndCongfig<type>() instead.
        /// Don't use it before configurating the mapper first.
        /// </summary>
        /// <param name="t"></param>
        public void StoreType(Type t)
        {
            if (!_TypesToMap.Contains(t))
                _TypesToMap.Add(t);
        }
        /// <summary>
        /// Store a mapper object in static dictionary.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="mapper"></param>
        public void StoreMapper(Type t, iDapperMapper mapper)
        {
            if (!_Mappers.ContainsKey(t)) _Mappers.Add(t, mapper);
        }
        /// <summary>
        /// Get mapper of type t. If type t haven't been stored by StoreType, returns null. If type t have been stored and there are no mapper
        /// created yet, it creates a new one, store it, and return it.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public iDapperMapper GetMapper(Type t)
        {
            if (_Mappers.ContainsKey(t))
                return _Mappers[t];

            if (!_TypesToMap.Contains(t))
                return null;

            iDapperMapper mapper = (iDapperMapper)Activator.CreateInstance(typeof(DapperMapper<>).MakeGenericType(t), this);
            StoreMapper(t, mapper);
            return mapper;
        }
        /// <summary>
        /// Returns true if a mapper exists or can be created, and set it as iDapperMapper.
        /// If type t haven't been stored by StoreType, returns false. If type t have been stored and there are no mapper
        /// created yet, it creates a new one, store it, set it as iDapperMapper and returns true.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="mapper"></param>
        /// <returns></returns>
        public bool GetMapper(Type t, out iDapperMapper mapper)
        {
            if (_Mappers.ContainsKey(t))
            {
                mapper = _Mappers[t];
                return true;
            }

            if (!_TypesToMap.Contains(t))
            {
                mapper = null;
                return false;
            }

            mapper = (iDapperMapper)Activator.CreateInstance(typeof(DapperMapper<>).MakeGenericType(t), this);
            StoreMapper(t, mapper);
            return false;
        }
        /// <summary>
        /// Remove mapper previously stored.
        /// </summary>
        /// <param name="t"></param>
        public void RemoveMapper(Type t)
        {
            if (_Mappers.ContainsKey(t))
                _Mappers.Remove(t);
        }
    }
}
