using System.Collections.Generic;
using UnityEngine;

public static class CharacterDatabase
{
    public static KillerType GetKillerByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        string path = "ScriptableObjects/Killers/" + name + "/" + name + " Type";
        Debug.Log("Loading Killer: " + name + " With path: " + path);
        return Resources.Load(path) as KillerType;
    }

    public static SurvivorType GetSurvivorByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        string path = "ScriptableObjects/Survivors/" + name + "/" + name + " Type";
        Debug.Log("Loading Survivor: " + name + " With path: " + path);
        return Resources.Load(path) as SurvivorType;
    }

    public static CubePlayerBase GetCharacterByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        KillerType killer = GetKillerByName(name);
        if (killer != null)
            return killer;

        return GetSurvivorByName(name);
    }

    public static KillerType[] GetAllKillers()
    {
        return Resources.LoadAll<KillerType>("ScriptableObjects/Killers");
    }

    public static SurvivorType[] GetAllSurvivors()
    {
        return Resources.LoadAll<SurvivorType>("ScriptableObjects/Survivors");
    }

    public static List<CubePlayerBase> GetAllCharacters()
    {
        var list = new List<CubePlayerBase>();
        var killers = GetAllKillers();
        if (killers != null)
        {
            for (int i = 0; i < killers.Length; i++)
            {
                if (killers[i] != null && !list.Contains(killers[i]))
                    list.Add(killers[i]);
            }
        }

        var survivors = GetAllSurvivors();
        if (survivors != null)
        {
            for (int i = 0; i < survivors.Length; i++)
            {
                if (survivors[i] != null && !list.Contains(survivors[i]))
                    list.Add(survivors[i]);
            }
        }

        return list;
    }
}

