// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// This attribute allows a parameter to accept multiple types by replacing a target type with the value
    /// of a specified property. If the argument is not of the target type, it will not be changed.
    /// </summary>
    /// <example>
    /// <code>
    /// [PropertyByTypeTransformation(Property = "Name", TypeToTransform = typeof(Zone))]
    /// public string ZoneName { get; set; }
    /// </code>
    /// Transforms any Zone objects given to the ZoneName Parameter into zoneObject.Name
    /// </example>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class PropertyByTypeTransformationAttribute : ArgumentTransformationAttribute
    {
        /// <summary>
        /// The target Type that will be transformed. e.g. A Google.Aips.Compute.v1.Zone object.
        /// </summary>
        public Type TypeToTransform { get; set; }

        /// <summary>
        /// The name of the Property/Field to return. e.g. Name, a property of the type Zone
        /// </summary>
        public string Property { get; set; }

        public override object Transform(EngineIntrinsics engineIntrinsics, object inputData)
        {
            // Treat PSObjects with a base type of the target type as the target type.
            if (inputData is PSObject && TypeToTransform.IsInstanceOfType((inputData as PSObject).BaseObject))
            {
                return (inputData as PSObject).Properties[Property].Value;
            }
            // Target type case
            else if (TypeToTransform.IsInstanceOfType(inputData))
            {
                return new PSObject(inputData).Properties[Property].Value;
            }
            else
            {
                return inputData;
            }
        }
    }

    /// <summary>
    /// This attribute indicates which property of the gcloud config provides the default for this parameter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class ConfigPropertyNameAttribute : Attribute
    {
        /// <summary>
        /// The gcloud config property that holds the default for this attribute.
        /// </summary>
        /// <example>
        /// project, zone
        /// </example>
        public string Property { get; }

        public ConfigPropertyNameAttribute(string property)
        {
            Property = property;
        }

        /// <summary>
        /// Gives the property a default value from the gcould config if needed.
        /// </summary>
        /// <param name="property">
        /// The property info.
        /// </param>
        /// <param name="instance">
        /// The instance the property is a member of.
        /// </param>
        public void SetConfigDefault(PropertyInfo property, GCloudCmdlet instance)
        {
            bool isBoundParameter = instance.MyInvocation.BoundParameters.ContainsKey(property.Name);
            if (!isBoundParameter)
            {
                SetObjectConfigDefault(property, instance);
            }
        }

        /// <summary>
        /// Gives the property a default value from the gcould config. This sets the property regardless of its
        /// current value.
        /// </summary>
        /// <param name="property">The property to set.</param>
        /// <param name="instance">The instance that contains the property to set.</param>
        public void SetObjectConfigDefault(PropertyInfo property, object instance)
        {
            string settingsValue = CloudSdkSettings.GetSettingsValue(Property);
            if (string.IsNullOrEmpty(settingsValue))
            {
                throw new PSInvalidOperationException(
                    $"Parameter {property.Name} was not set and does not have a default value.");
            }

            property.SetValue(instance, settingsValue);
        }

        /// <summary>
        /// Gives the field a default value from the gcould config.
        /// </summary>
        /// <param name="field">
        /// The field info.
        /// </param>
        /// <param name="instance">
        /// The instance the field is a member of.
        /// </param>
        public void SetConfigDefault(FieldInfo field, GCloudCmdlet instance)
        {
            bool isBoundParameter = instance.MyInvocation.BoundParameters.ContainsKey(field.Name);
            if (!isBoundParameter)
            {
                string settingsValue = CloudSdkSettings.GetSettingsValue(Property);
                if (string.IsNullOrEmpty(settingsValue))
                {
                    throw new PSInvalidOperationException(
                        $"Parameter {field.Name} was not set and does not have a default value.");
                }

                field.SetValue(instance, settingsValue);
            }
        }
    }
    /// <summary>
    /// This attribute allows an array parameter to accept multiple types by replacing a target type with the 
    /// value of a specified property. If the array element is not of the target type, it will not be changed.
    /// </summary>
    /// <example>
    /// <code>
    /// [ArrayPropertyTransform(typeof(Zone), nameof(Zone.Name))]
    /// public string[] ZoneName { get; set; }
    /// </code>
    /// Transforms any Zone objects given to the ZoneName Parameter into zoneObject.Name
    /// </example>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class ArrayPropertyTransformAttribute : ArgumentTransformationAttribute
    {
        private PropertyByTypeTransformationAttribute _typeTransformationAttribute;

        public ArrayPropertyTransformAttribute(Type typeToTransform, string property)
        {
            _typeTransformationAttribute = new PropertyByTypeTransformationAttribute
            {
                Property = property,
                TypeToTransform = typeToTransform
            };
        }

        public override object Transform(EngineIntrinsics engineIntrinsics, object inputData)
        {
            var enumerable = inputData as IEnumerable<object> ?? new[] { inputData };
            return enumerable.Select(
                    inputElement => _typeTransformationAttribute.Transform(engineIntrinsics, inputElement)
                    ).ToArray();
        }
    }
}
