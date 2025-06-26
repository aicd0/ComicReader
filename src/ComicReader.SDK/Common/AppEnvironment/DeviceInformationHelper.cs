﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

// Licensed to the Microsoft Corporation under one or more agreements.
// The Microsoft Corporation licenses this file to you under the MIT license.

#nullable disable

using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.Win32;

namespace ComicReader.SDK.Common.AppEnvironment;

internal class DeviceInformationHelper
{
    public static string DefaultSystemManufacturer = "System manufacturer";
    public static string DefaultSystemProductName = "System Product Name";

    private static DeviceInformationHelper _instanceField;
    internal static DeviceInformationHelper Instance => _instanceField ??= new DeviceInformationHelper();

    private readonly ManagementClassFactory _managmentClassFactory;

    public DeviceInformationHelper()
    {
        _managmentClassFactory = ManagementClassFactory.Instance;
    }

    public string GetDeviceModel()
    {
        try
        {
            using ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = _managmentClassFactory.GetComputerSystemClass().GetInstances().GetEnumerator();
            if (managementObjectEnumerator.MoveNext())
            {
                string text = (string)managementObjectEnumerator.Current["Model"];
                return string.IsNullOrEmpty(text) || DefaultSystemProductName == text ? null : text;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
        catch (COMException)
        {
            return string.Empty;
        }
        catch (ManagementException)
        {
            return string.Empty;
        }
        catch (PlatformNotSupportedException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    public string GetAppNamespace()
    {
        return Assembly.GetEntryAssembly()?.EntryPoint.DeclaringType?.Namespace;
    }

    public string GetDeviceOemName()
    {
        try
        {
            using ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = _managmentClassFactory.GetComputerSystemClass().GetInstances().GetEnumerator();
            if (managementObjectEnumerator.MoveNext())
            {
                string text = (string)managementObjectEnumerator.Current["Manufacturer"];
                return string.IsNullOrEmpty(text) || DefaultSystemManufacturer == text ? null : text;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
        catch (COMException)
        {
            return string.Empty;
        }
        catch (ManagementException)
        {
            return string.Empty;
        }
        catch (PlatformNotSupportedException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    public string GetOsBuild()
    {
        using RegistryKey registryKey = Registry.LocalMachine;
        using RegistryKey registryKey2 = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");
        object value = registryKey2.GetValue("CurrentMajorVersionNumber");
        if (value != null)
        {
            object value2 = registryKey2.GetValue("CurrentMinorVersionNumber", "0");
            object value3 = registryKey2.GetValue("CurrentBuildNumber", "0");
            object value4 = registryKey2.GetValue("UBR", "0");
            return $"{value}.{value2}.{value3}.{value4}";
        }

        object value5 = registryKey2.GetValue("CurrentVersion", "0.0");
        object value6 = registryKey2.GetValue("CurrentBuild", "0");
        string[] array = registryKey2.GetValue("BuildLabEx")?.ToString().Split('.');
        string value7 = array != null && array.Length >= 2 ? array[1] : "0";
        return $"{value5}.{value6}.{value7}";
    }

    public string GetOsVersion()
    {
        try
        {
            using ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = _managmentClassFactory.GetOperatingSystemClass().GetInstances().GetEnumerator();
            if (managementObjectEnumerator.MoveNext())
            {
                return (string)managementObjectEnumerator.Current["Version"];
            }
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
        catch (COMException)
        {
            return string.Empty;
        }
        catch (ManagementException)
        {
            return string.Empty;
        }
        catch (PlatformNotSupportedException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    public string GetScreenSize()
    {
        WindowsHelper.GetScreenSize(out int width, out int height);
        return $"{width}x{height}";
    }
}
