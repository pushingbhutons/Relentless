// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/



using System.Collections;
using System.Collections.Generic;
using LoomNetwork.CZB.Common;
using Newtonsoft.Json;
using System;

namespace LoomNetwork.CZB.Data
{
    public class AbilityData
    {
        public string type;
        public string activityType;
        public string callType;
        public string targetType;
        public string statType;
        public string setType;
        public string effectType;
        public string cardType;
        public string unitStatus;

        [JsonIgnore]
		public Enumerators.AbilityType abilityType;
        [JsonIgnore]
		public Enumerators.AbilityActivityType abilityActivityType;
        [JsonIgnore]
		public Enumerators.AbilityCallType abilityCallType;
        [JsonIgnore]
		public List<Enumerators.AbilityTargetType> abilityTargetTypes;
        [JsonIgnore]
		public Enumerators.StatType abilityStatType;
        [JsonIgnore]
        public Enumerators.SetType abilitySetType;
        [JsonIgnore]
        public Enumerators.AbilityEffectType abilityEffectType;
        [JsonIgnore]
        public Enumerators.AttackInfoType attackInfoType;
        [JsonIgnore]
        public Enumerators.CardType targetCardType;
        [JsonIgnore]
        public Enumerators.UnitStatusType targetUnitStatusType; 

        public int value;

        public int damage;
        public int health;
        public string attackInfo;

        public AbilityData()
        {

        }

        public void ParseData()
        {
            abilityType = CastStringTuEnum<Enumerators.AbilityType>(type);
            abilityActivityType = CastStringTuEnum<Enumerators.AbilityActivityType>(activityType);
            abilityCallType = CastStringTuEnum<Enumerators.AbilityCallType>(callType);

            if (!string.IsNullOrEmpty(targetType))
                abilityTargetTypes = CastList<Enumerators.AbilityTargetType>(targetType);

            if (!string.IsNullOrEmpty(statType))
                abilityStatType = CastStringTuEnum<Enumerators.StatType>(statType);

            if (!string.IsNullOrEmpty(setType))
                abilitySetType = CastStringTuEnum<Enumerators.SetType>(setType);

            if (!string.IsNullOrEmpty(effectType))
                abilityEffectType = CastStringTuEnum<Enumerators.AbilityEffectType>(effectType);

            if (!string.IsNullOrEmpty(attackInfo))
                attackInfoType = CastStringTuEnum<Enumerators.AttackInfoType>(attackInfo);

            if (!string.IsNullOrEmpty(cardType))
                targetCardType = CastStringTuEnum<Enumerators.CardType>(cardType);


            if (!string.IsNullOrEmpty(unitStatus))
                targetUnitStatusType = CastStringTuEnum<Enumerators.UnitStatusType>(unitStatus);
        }

        private T CastStringTuEnum<T>(string data)
        {
            //UnityEngine.Debug.Log(typeof(T) + " | " + data);
            return (T)Enum.Parse(typeof(T), data.ToUpper());
        }

        private List<T> CastList<T>(string data, char separator = '|')
        {
            List<T> list = new List<T>();
            string[] targets = data.Split(separator);
            foreach(var target in targets)
            {
                list.Add(CastStringTuEnum<T>(target));
            }
            return list;
        }
    }
}
