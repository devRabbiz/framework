﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Entities;
using Signum.Utilities;
using Signum.Utilities.Reflection;
using System.Reflection;
using System.Linq.Expressions;
using System.Data;
using Signum.Entities.Reflection;
using Microsoft.SqlServer.Types;
using Microsoft.SqlServer.Server;

namespace Signum.Engine.Maps
{
    public enum DBMS
    {
        SqlCompact,
        SqlServer2005,
        SqlServer2008,
        SqlServer2012,
    }

    public class SchemaSettings
    {
        public SchemaSettings()
        { 

        }

        public SchemaSettings(DBMS dbms)
        {
            DBMS = dbms;
            if (dbms >= Maps.DBMS.SqlServer2008)
            {
                TypeValues.Add(typeof(TimeSpan), SqlDbType.Time);

                UdtSqlName.Add(typeof(SqlHierarchyId), "HierarchyId");
                UdtSqlName.Add(typeof(SqlGeography), "Geography");
                UdtSqlName.Add(typeof(SqlGeometry), "Geometry");
            }
        }


        public int MaxNumberOfParameters = 2000;
        public int MaxNumberOfStatementsInSaveQueries = 16; 

        public DBMS DBMS { get; private set; }

        public Dictionary<PropertyRoute, Attribute[]> OverridenAttributes = new Dictionary<PropertyRoute, Attribute[]>();

        public Dictionary<Type, string> UdtSqlName = new Dictionary<Type, string>()
        {

        };

        public Dictionary<Type, SqlDbType> TypeValues = new Dictionary<Type, SqlDbType>
        {
            {typeof(bool), SqlDbType.Bit},

            {typeof(byte), SqlDbType.TinyInt},
            {typeof(short), SqlDbType.SmallInt},
            {typeof(int), SqlDbType.Int},
            {typeof(long), SqlDbType.BigInt},

            {typeof(float), SqlDbType.Real},
            {typeof(double), SqlDbType.Float},
            {typeof(decimal), SqlDbType.Decimal},

            {typeof(char), SqlDbType.NChar},
            {typeof(string), SqlDbType.NVarChar},
            {typeof(DateTime), SqlDbType.DateTime},

            {typeof(Byte[]), SqlDbType.VarBinary},

            {typeof(Guid), SqlDbType.UniqueIdentifier},
        };

        internal Dictionary<Type, string> desambiguatedNames;

        Dictionary<SqlDbType, int> defaultSize = new Dictionary<SqlDbType, int>()
        {
            {SqlDbType.NVarChar, 200}, 
            {SqlDbType.VarChar, 200}, 
            {SqlDbType.Image, 8000}, 
            {SqlDbType.VarBinary, int.MaxValue}, 
            {SqlDbType.Binary, 8000}, 
            {SqlDbType.Char, 1}, 
            {SqlDbType.NChar, 1}, 
            {SqlDbType.Decimal, 18}, 
        };

        Dictionary<SqlDbType, int> defaultScale = new Dictionary<SqlDbType, int>()
        {
            {SqlDbType.Decimal, 2}, 
        };

        public bool IsOverriden<T>(Expression<Func<T, object>> route) where T : IdentifiableEntity
        {
            return IsOverriden(PropertyRoute.Construct(route));
        }

        private bool IsOverriden(PropertyRoute route)
        {
            return OverridenAttributes.ContainsKey(route);
        }

        public void OverrideAttributes<T>(Expression<Func<T, object>> route, params Attribute[] attributes)
            where T : IdentifiableEntity
        {
            OverrideAttributes(PropertyRoute.Construct(route), attributes);
        }

        public void OverrideAttributes(PropertyRoute route, params Attribute[] attributes)
        {
            AssertCorrect(attributes, AttributeTargets.Field);

            OverridenAttributes.Add(route, attributes);
        }

        private void AssertCorrect(Attribute[] attributes, AttributeTargets attributeTargets)
        {
            var incorrects = attributes.Where(a => a.GetType().SingleAttribute<AttributeUsageAttribute>().TryCS(au => (au.ValidOn & attributeTargets) == 0) ?? false);

            if (incorrects.Count() > 0)
                throw new InvalidOperationException("The following attributes ar not compatible with targets {0}: {1}".Formato(attributeTargets, incorrects.ToString(a => a.GetType().Name, ", ")));
        }

        public Attribute[] Attributes<T>(Expression<Func<T, object>> route)
        {
            return Attributes(route);
        }

        public Attribute[] Attributes(PropertyRoute route)
        {
            if(route.PropertyRouteType == PropertyRouteType.Root || route.PropertyRouteType == PropertyRouteType.LiteEntity)
                throw new InvalidOperationException("Route of type {0} not supported for this method".Formato(route.PropertyRouteType));

            var overriden = OverridenAttributes.TryGetC(route); 

            if(overriden!= null)
                return overriden; 

            switch (route.PropertyRouteType)
	        {
                case PropertyRouteType.FieldOrProperty: 
                    return route.FieldInfo.GetCustomAttributes(false).Cast<Attribute>().ToArray(); 
                case PropertyRouteType.MListItems: 
                    return route.Parent.FieldInfo.GetCustomAttributes(false).Cast<Attribute>().ToArray();
                default:
                    throw new InvalidOperationException("Route of type {0} not supported for this method".Formato(route.PropertyRouteType));
	        }
        }

        internal bool IsNullable(PropertyRoute route, bool forceNull)
        {
            if (forceNull)
                return true;

            var attrs = Attributes(route);

            if (attrs.OfType<NotNullableAttribute>().Any())
                return false;

            if (attrs.OfType<NullableAttribute>().Any())
                return true;

            return !route.Type.IsValueType || route.Type.IsNullable();
        }

        internal IndexType GetIndexType(PropertyRoute route)
        {
            UniqueIndexAttribute at = Attributes(route).OfType<UniqueIndexAttribute>().SingleOrDefaultEx();

            return at == null ? IndexType.None :
                at.AllowMultipleNulls ? IndexType.UniqueMultipleNulls :
                IndexType.Unique;
        }

        public bool ImplementedBy<T>(Expression<Func<T, object>> route, Type typeToImplement) where T : IdentifiableEntity
        {
            var imp = GetImplementations(route);
            return !imp.IsByAll  && imp.Types.Contains(typeToImplement);
        }

        public void AssertImplementedBy<T>(Expression<Func<T, object>> route, Type typeToImplement) where T : IdentifiableEntity
        {
            var propRoute = PropertyRoute.Construct(route);

            Implementations imp = GetImplementations(propRoute);

            if (imp.IsByAll || !imp.Types.Contains(typeToImplement))
                throw new InvalidOperationException("Route {0} is not ImplementedBy {1}".Formato(propRoute, typeToImplement.Name));
        }

        public Implementations GetImplementations<T>(Expression<Func<T, object>> route) where T : IdentifiableEntity
        {
            return GetImplementations(PropertyRoute.Construct(route));
        }

        public Implementations GetImplementations(PropertyRoute route)
        {
            var cleanType = route.Type.CleanType();  
            if (!route.Type.CleanType().IsIIdentifiable())
                throw new InvalidOperationException("{0} is not a {1}".Formato(route, typeof(IIdentifiable).Name));

            var fieldAtt = Attributes(route);

            return Implementations.FromAttributes(cleanType, fieldAtt, route);
        }

        internal SqlDbTypePair GetSqlDbType(PropertyRoute route)
        {
            SqlDbTypeAttribute att = Attributes(route).OfType<SqlDbTypeAttribute>().SingleOrDefaultEx();

            if (att != null && att.HasSqlDbType)
                return new SqlDbTypePair(att.SqlDbType, att.UdtTypeName);

            return GetSqlDbTypePair(route.Type.UnNullify());
        }

        internal int? GetSqlSize(PropertyRoute route, SqlDbType sqlDbType)
        {
            SqlDbTypeAttribute att = Attributes(route).OfType<SqlDbTypeAttribute>().SingleOrDefaultEx();

            if (att != null && att.HasSize)
                return att.Size;

            return defaultSize.TryGetS(sqlDbType);
        }

        internal int? GetSqlScale(PropertyRoute route, SqlDbType sqlDbType)
        {
            SqlDbTypeAttribute att = Attributes(route).OfType<SqlDbTypeAttribute>().SingleOrDefaultEx();

            if (att != null && att.HasScale)
                return att.Scale;

            return defaultScale.TryGetS(sqlDbType);
        }

        internal SqlDbType DefaultSqlType(Type type)
        {
            return this.TypeValues.GetOrThrow(type, "Type {0} not registered");
        }

        public void Desambiguate(Type type, string cleanName)
        {
            if (desambiguatedNames != null)
                desambiguatedNames = new Dictionary<Type, string>();

            desambiguatedNames[type] = cleanName;
        }

        internal void FixType(ref SqlDbType type, ref int? size, ref int? scale)
        {
            if (DBMS == Maps.DBMS.SqlCompact && (type == SqlDbType.NVarChar || type == SqlDbType.VarChar) && size > 4000)
            {
                type = SqlDbType.NText;
                size = null;
            }
        }

        public SqlDbTypePair GetSqlDbTypePair(Type type)
        {
            SqlDbType result;
            if (TypeValues.TryGetValue(type, out result))
                return new SqlDbTypePair(result, null);

            string udtTypeName = GetUdtName(type);
            if (udtTypeName != null)
                return new SqlDbTypePair(SqlDbType.Udt, udtTypeName);

            return null;
        }

        public string GetUdtName(Type udtType)
        {
            var att = udtType.SingleAttribute<SqlUserDefinedTypeAttribute>();

            if (att == null)
                return null;

            return UdtSqlName[udtType];
        }

        public bool IsDbType(Type type)
        {
            return type.IsEnum || GetSqlDbTypePair(type) != null;
        }

    }

    public class SqlDbTypePair
    {
        public SqlDbType SqlDbType { get; private set; }
        public string UdtTypeName { get; private set; }

        public SqlDbTypePair() { }

        public SqlDbTypePair(SqlDbType type, string udtTypeName)
        {
            this.SqlDbType = type;
            this.UdtTypeName = udtTypeName;
        }
    }

    internal enum ReferenceFieldType
    {
        Reference,
        ImplementedBy,
        ImplmentedByAll,
    }
}
