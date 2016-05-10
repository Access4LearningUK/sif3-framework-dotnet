﻿/*
 * Crown Copyright © Department for Education (UK) 2016
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Sif.Framework.Model.DataModels;
using Sif.Framework.Model.Persistence;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Sif.Framework.Model.Infrastructure
{

    /// <summary>
    /// The following Job elements/properties are mandatory according to the SIF specification:
    /// /job[@id]
    /// /job/name
    /// </summary>
    public class Job : IPersistable<Guid>
    {

        /// <summary>
        /// The ID of the Job as managed by the Functional Service.
        /// </summary>
        public virtual Guid Id { get; set; }
        
        /// <summary>
        /// The name of the job, e.g. "grading" or "sre".
        /// </summary>
        public virtual string Name { get; set; }

        /// <summary>
        /// A description of the job, e.g. "Bowers Elementary School Final Marks"
        /// </summary>
        public virtual string Description { get; set; }

        /// <summary>
        /// The current enumerable state of the job.
        /// </summary>
        public virtual JobStateType? State { get; set; }

        /// <summary>
        /// A descriptive message ellaborating on the current state, e.g. if the current state is "FAILED" the stateDescription may be "Timeout occured".
        /// </summary>
        public virtual string StateDescription { get; set; }

        /// <summary>
        /// The datetime this job was created.
        /// </summary>
        public virtual DateTime? Created { get; set; }

        /// <summary>
        /// The datetime this job was last modified.
        /// </summary>
        public virtual DateTime? LastModified { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public virtual IDictionary<string, Phase> Phases { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public Job()
        {
            Created = DateTime.UtcNow;
            LastModified = Created;
            Phases = new Dictionary<string, Phase>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        public Job(string name, string description = null) : this()
        {
            if (String.IsNullOrWhiteSpace(name) || name.Contains(" "))
            {
                throw new ArgumentNullException("name", "Job must have a name, which cannot contain any spaces");
            }
            Name = name;

            if (!String.IsNullOrWhiteSpace(description))
            {
                Description = description;
            }
        }

        public virtual void changeState(JobStateType type, string description = null)
        {
            LastModified = DateTime.UtcNow;
            StateDescription = description;
            State = type;
        }

        public virtual void addPhase(Phase phase)
        {
            Phases.Add(phase.Name, phase);
        }

        public virtual void updatePhaseState(string phaseName, PhaseStateType state, string stateDescription = null)
        {
            State s = Phases[phaseName].changeState(state, stateDescription);
            LastModified = s.LastModified;
        }
    }

}