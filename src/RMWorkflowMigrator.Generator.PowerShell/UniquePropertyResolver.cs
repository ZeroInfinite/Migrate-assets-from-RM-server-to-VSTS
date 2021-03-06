﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UniquePropertyResolver.cs" company="Microsoft Corporation">
//   Copyright Microsoft Corporation. All Rights Reserved. This code released under the terms of the MIT License (MIT, https://github.com/ALM-Rangers/Migrate-assets-from-RM-server-to-VSO/blob/master/License.txt). This is sample code only, do not use in production environments.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Microsoft.ALMRangers.RMWorkflowMigrator.Generator.PowerShell
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.ALMRangers.RMWorkflowMigrator.DataAccess.Model;
    using Microsoft.ALMRangers.RMWorkflowMigrator.Generator.PowerShell.Model;
    using Microsoft.ALMRangers.RMWorkflowMigrator.Parser;
    using Microsoft.ALMRangers.RMWorkflowMigrator.Parser.Model;

    public static class UniquePropertyResolver
    {
        public static IEnumerable<ScriptAction> ResolveProperties(IEnumerable<ScriptAction> actions)
        {
            var sequence = 2;
            var properties = new HashSet<Tuple<string, string, string>>();
            foreach (var action in actions)
            {
                action.Arguments = CleanActionParameters(action.Arguments);
                action.Command = CleanActionParameters(action.Command);
                CleanConfigurationValues(action.ConfigurationVariables);
                sequence = MakeParametersUnique(action, properties, sequence);
            }

            return actions;
        }

        private static int MakeParametersUnique(ScriptAction action, ISet<Tuple<string, string, string>> properties, int sequence)
        {
            if (action.ConfigurationVariables == null)
            {
                return sequence;
            }

            foreach (var configVar in action.ConfigurationVariables)
            {
                var tuple = Tuple.Create(CleanInvalidCharacters(configVar.OriginalName), configVar.Value, configVar.RemappedName);

                // If we already have a matching variable name + value, we can reuse the remapped name of what's there
                var match = properties.FirstOrDefault(p => p.Item1 == tuple.Item1 && p.Item2 == tuple.Item2);
                if (match != null)
                {
                    UpdateRemappedVariable(action, configVar, match.Item3);
                }
                // If we have a matching variable name with a different value, remap it with a sequence number
                else if (properties.Any(p => p.Item1 == tuple.Item1 && p.Item2 != tuple.Item2))
                {
                    var newVariableName = configVar.RemappedName + sequence;
                    sequence++;
                    UpdateRemappedVariable(action, configVar, newVariableName);
                    tuple = Tuple.Create(CleanInvalidCharacters(configVar.OriginalName), configVar.Value, newVariableName);
                    properties.Add(tuple);
                }
                // Otherwise, we've never seen this variable + value combo before so we can just track that we've seen it
                else
                {
                    properties.Add(tuple);
                }
                
            }

            return sequence;
        }

        private static void UpdateRemappedVariable(ScriptAction action, ConfigurationVariable configVar, string newVariableName)
        {
            action.Arguments = action.Arguments?.Replace($"${configVar.RemappedName}", $"${newVariableName}");
            var commandHasParameter = action.Command?.Contains(configVar.RemappedName);
            if (commandHasParameter.HasValue && commandHasParameter.Value)
            {
                action.Command = action.Command?.Replace($"${configVar.RemappedName}", $"${newVariableName}");
            }
            configVar.RemappedName = newVariableName;
        }

        private static void CleanConfigurationValues(IEnumerable<ConfigurationVariable> configurationVariables)
        {
            if (configurationVariables == null)
            {
                return;
            }

            foreach (var configVar in configurationVariables)
            {
                configVar.RemappedName = CleanInvalidCharacters(configVar.OriginalName);
            }
        }

        private static string CleanActionParameters(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return arguments;
            }

            var matches = CommonRegex.ParameterRegex.Matches(arguments);
            foreach (var match in matches)
            {
                var replacement = match.ToString().Replace("__", string.Empty);
                replacement = "$" + CleanInvalidCharacters(replacement);
                arguments  = arguments.Replace(match.ToString(), replacement);
            }

            return arguments;
        }

        private static string CleanInvalidCharacters(string value)
        {
            return CommonRegex.InvalidCharactersRegex.Replace(value, string.Empty);
        }
    }
}
