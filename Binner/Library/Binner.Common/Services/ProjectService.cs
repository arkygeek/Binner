﻿using AnyMapper;
using Binner.Common.Models;
using Binner.Common.Models.Requests;
using Binner.Model.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Binner.Common.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IStorageProvider _storageProvider;
        private readonly RequestContextAccessor _requestContext;

        public ProjectService(IStorageProvider storageProvider, RequestContextAccessor requestContextAccessor)
        {
            _storageProvider = storageProvider;
            _requestContext = requestContextAccessor;
        }

        public async Task<Project> AddProjectAsync(Project project)
        {
            return await _storageProvider.AddProjectAsync(project, _requestContext.GetUserContext());
        }

        public async Task<bool> DeleteProjectAsync(Project project)
        {
            var user = _requestContext.GetUserContext();
            // delete any pcb assignments
            var pcbAssignments = await _storageProvider.GetProjectPcbAssignmentsAsync(project.ProjectId, user);
            foreach (var pcbAssignment in pcbAssignments)
            {
                // delete any stored file assignments associated with pcb
                var storedFileAssignments = await _storageProvider.GetPcbStoredFileAssignmentsAsync(pcbAssignment.PcbId, user);
                foreach (var storedFileAssignment in storedFileAssignments)
                {
                    await _storageProvider.RemovePcbStoredFileAssignmentAsync(storedFileAssignment, user);
                }

                await _storageProvider.RemoveProjectPcbAssignmentAsync(pcbAssignment, user);
            }
            // remove any part assignments associated with this project
            var projectPartAssignments = await _storageProvider.GetProjectPartAssignmentsAsync(project.ProjectId, user);
            foreach (var partAssignment in projectPartAssignments)
            {
                await _storageProvider.RemoveProjectPartAssignmentAsync(partAssignment, user);
            }

            var success = await _storageProvider.DeleteProjectAsync(project, user);
            return success;
        }

        public async Task<Project?> GetProjectAsync(long projectId)
        {
            return await _storageProvider.GetProjectAsync(projectId, _requestContext.GetUserContext());
        }

        public async Task<Project?> GetProjectAsync(string name)
        {
            return await _storageProvider.GetProjectAsync(name, _requestContext.GetUserContext());
        }

        public async Task<ICollection<Project>> GetProjectsAsync(PaginatedRequest request)
        {
            return await _storageProvider.GetProjectsAsync(request, _requestContext.GetUserContext());
        }

        public async Task<Project> UpdateProjectAsync(Project project)
        {
            project.DateModifiedUtc = DateTime.UtcNow;
            return await _storageProvider.UpdateProjectAsync(project, _requestContext.GetUserContext());
        }

        public async Task<ICollection<ProjectPart>> GetPartsAsync(long projectId)
        {
            var user = _requestContext.GetUserContext();
            var parts = new List<ProjectPart>();
            var assignments = await _storageProvider.GetProjectPartAssignmentsAsync(projectId, user);
            foreach (var assignment in assignments)
            {
                var projectPart = Mapper.Map<ProjectPart>(assignment);
                if (assignment.PartId != null)
                {
                    var part = await _storageProvider.GetPartAsync(assignment.PartId.Value, user);
                    if (part != null)
                    {
                        projectPart.Part = Mapper.Map<PartResponse>(part);
                        parts.Add(projectPart);
                    }
                }
                else if (!string.IsNullOrEmpty(assignment.PartName))
                {
                    parts.Add(projectPart);
                }
            }

            return parts;
        }

        public async Task<ICollection<ProjectPcb>> GetPcbsAsync(long projectId)
        {
            var user = _requestContext.GetUserContext();
            var projectPcbs = new List<ProjectPcb>();
            var pcbs = await _storageProvider.GetPcbsAsync(projectId, user);
            var projectParts = await _storageProvider.GetProjectPartAssignmentsAsync(projectId, user);
            foreach (var pcb in pcbs)
            {
                var projectPcb = Mapper.Map<ProjectPcb>(pcb);
                // get parts count for pcb
                projectPcb.PartsCount = projectParts.Count(x => x.PcbId == pcb.PcbId);
                projectPcbs.Add(projectPcb);
            }
            return projectPcbs;
        }

        /// <summary>
        /// Add a part to the project (BOM)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<ProjectPart?> AddPartAsync(AddBomPartRequest request)
        {
            Project? project = null;
            var user = _requestContext.GetUserContext();

            if (request.ProjectId != null)
                project = await _storageProvider.GetProjectAsync(request.ProjectId.Value, user);
            else if (!string.IsNullOrEmpty(request.Project))
                project = await _storageProvider.GetProjectAsync(request.Project, user);

            if (project == null)
                return null;

            var part = await _storageProvider.GetPartAsync(request.PartNumber, user);
            var assignment = new ProjectPartAssignment
            {
                PartId = part?.PartId,
                ProjectId = project.ProjectId,
                Notes = request.Notes,
                PartName = request.PartNumber,
                PcbId = request.PcbId,
                Cost = request.Cost,
                Currency = request.Currency,
                Quantity = request.Quantity,
                QuantityAvailable = part == null ? request.QuantityAvailable : 0,
                ReferenceId = request.ReferenceId,
                CustomDescription = request.CustomDescription,
                SchematicReferenceId = request.SchematicReferenceId,
            };
            await _storageProvider.AddProjectPartAssignmentAsync(assignment, user);
            // update project (DateModified)
            project.DateModifiedUtc = DateTime.UtcNow;
            await _storageProvider.UpdateProjectAsync(project, user);

            var projectPart = Mapper.Map<ProjectPart>(assignment);
            projectPart.Part = Mapper.Map<PartResponse>(part);
            return projectPart;
        }

        /// <summary>
        /// Update part details in project (BOM)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<ProjectPart?> UpdatePartAsync(UpdateBomPartRequest request)
        {
            var user = _requestContext.GetUserContext();
            var project = await _storageProvider.GetProjectAsync(request.ProjectId, user);
            if (project == null)
                return null;

            Part? part = null;
            if (request.PartId != null)
                part = await _storageProvider.GetPartAsync(request.PartId.Value, user);
            else if (!string.IsNullOrEmpty(request.PartName))
                part = await _storageProvider.GetPartAsync(request.PartName, user);

            var assignment = await _storageProvider.GetProjectPartAssignmentAsync(request.ProjectPartAssignmentId, user);
            if (assignment != null)
            {
                assignment.PartName = request.PartName;
                assignment.PartId = part?.PartId;
                assignment.Notes = request.Notes;
                assignment.ReferenceId = request.ReferenceId;
                assignment.Cost = request.Cost;
                assignment.Currency = request.Currency;
                assignment.Quantity = request.Quantity;
                assignment.CustomDescription = request.CustomDescription;
                assignment.SchematicReferenceId = request.SchematicReferenceId;

                if (part == null)
                {
                    assignment.QuantityAvailable = request.QuantityAvailable;
                    assignment.Cost = request.Cost;
                    assignment.Currency = request.Currency;
                }
                else
                {
                    assignment.QuantityAvailable = 0;
                    assignment.Cost = 0;
                    assignment.Currency = null;
                }

                await _storageProvider.UpdateProjectPartAssignmentAsync(assignment, user);
                
                // also update the part quantity and cost if it has changed
                if (request.Part != null && part != null)
                {
                    if (request.Part.Cost != part.Cost)
                        part.Cost = request.Part.Cost;
                    if (request.Part.Quantity >= 0 && request.Part.Quantity != part.Quantity)
                        part.Quantity = request.Part.Quantity;
                    await _storageProvider.UpdatePartAsync(part, user);
                }

                // update project (DateModified)
                project.DateModifiedUtc = DateTime.UtcNow;
                await _storageProvider.UpdateProjectAsync(project, user);

                var projectPart = Mapper.Map<ProjectPart>(assignment);
                projectPart.Part = Mapper.Map<PartResponse>(part);
                return projectPart;
            }

            return null;
        }

        /// <summary>
        /// Remove part from project (BOM)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<bool> RemovePartAsync(RemoveBomPartRequest request)
        {
            Project? project = null;
            var user = _requestContext.GetUserContext();

            if (!request.Ids.Any())
                return false;
            if (request.ProjectId != null)
                project = await _storageProvider.GetProjectAsync(request.ProjectId.Value, user);
            else if (!string.IsNullOrEmpty(request.Project))
                project = await _storageProvider.GetProjectAsync(request.Project, user);
            if (project == null) return false;

            var success = false;
            foreach (var projectPartAssignmentId in request.Ids)
            {
                var assignment = await _storageProvider.GetProjectPartAssignmentAsync(projectPartAssignmentId, user);
                if (assignment == null) continue;
                if (await _storageProvider.RemoveProjectPartAssignmentAsync(assignment, user))
                    success = true;
            }

            // update project (DateModified)
            project.DateModifiedUtc = DateTime.UtcNow;
            await _storageProvider.UpdateProjectAsync(project, user);

            return success;
        }

        public async Task<bool> ProducePcbAsync(ProduceBomPcbRequest request)
        {
            // get all the parts in the project
            var user = _requestContext.GetUserContext();
            var numberOfPcbsProduced = request.Quantity;

            var project = await GetProjectAsync(request.ProjectId);
            if (project == null)
                return false;

            var parts = await GetPartsAsync(request.ProjectId);

            // because some storage providers don't have transaction support, first validate we have the parts/quantities before making any changes
            await ProcessPcbParts(false);
            if (request.Unassociated)
                await ProcessNonPcbParts(false);

            // no exceptions thrown, write the changes
            await ProcessPcbParts(true);
            if (request.Unassociated)
                await ProcessNonPcbParts(true);

            // update project (DateModified)
            project.DateModifiedUtc = DateTime.UtcNow;
            await _storageProvider.UpdateProjectAsync(project, user);

            return true;

            async Task ProcessPcbParts(bool performUpdates)
            {
                foreach (var pcb in request.Pcbs)
                {
                    var pcbEntity = await _storageProvider.GetPcbAsync(pcb.PcbId, user);
                    if (pcbEntity == null)
                        throw new InvalidOperationException($"The pcb with Id '{pcb.PcbId}' could not be found!");

                    // get the parts for this pcb in the BOM
                    var pcbParts = parts.Where(x => x.PcbId != null && x.PcbId == pcb.PcbId).ToList();
                    foreach (var pcbPart in pcbParts)
                    {
                        if (pcbPart.Part == null)
                            throw new ArgumentNullException($"Invalid request: Part object cannot be null!");

                        // get the quantity to remove, which is the number of parts used on this pcb X the number of pcb boards produced
                        var quantityToRemove = pcbPart.Quantity * numberOfPcbsProduced;
                        if (quantityToRemove > pcbPart.Part.Quantity)
                            throw new InvalidOperationException($"There are not enough parts in inventory for part: {pcbPart.PartName}. In Stock: {pcbPart.Part.Quantity}, Quantity needed: {quantityToRemove}");

                        if (performUpdates)
                        {
                            // if the pcb has a quantity > 1, it acts as a multiplier for BOMs that produce multiples of a single PCB
                            // a value of 0 is invalid
                            if (pcbEntity.Quantity > 1)
                                pcbPart.Part.Quantity -= (quantityToRemove * pcbEntity.Quantity);
                            else
                                pcbPart.Part.Quantity -= quantityToRemove;
                            await _storageProvider.UpdatePartAsync(Mapper.Map<Part>(pcbPart.Part), user);
                        }
                    }

                    if (performUpdates)
                    {
                        pcbEntity.LastSerialNumber = IncrementSerialNumber(pcb.SerialNumber ?? string.Empty, numberOfPcbsProduced);
                        await _storageProvider.UpdatePcbAsync(pcbEntity, user);
                    }
                }
            }

            async Task ProcessNonPcbParts(bool performUpdates)
            {
                foreach (var part in parts.Where(x => x.PcbId == null))
                {
                    if (part.Part == null)
                        throw new ArgumentNullException($"Invalid request: Part object cannot be null!");

                    // get the quantity to remove, which is the number of parts used on this pcb X the number of pcb boards produced
                    var quantityToRemove = part.Quantity * numberOfPcbsProduced;
                    if (quantityToRemove > part.Part.Quantity)
                        throw new InvalidOperationException(
                            $"There are not enough parts in inventory for part: {part.PartName}. In Stock: {part.Part.Quantity}, Quantity needed: {quantityToRemove}");

                    if (performUpdates)
                    {
                        part.Part.Quantity -= quantityToRemove;
                        await _storageProvider.UpdatePartAsync(Mapper.Map<Part>(part.Part), user);
                    }
                }
            }
        }

        public string IncrementSerialNumber(string nextSerialNumber, int numberOfPcbsProduced)
        {
            var serialNumber = nextSerialNumber;
            for (var s = 1; s < numberOfPcbsProduced; s++)
            {
                // add 1 to the serial number

                // find the index of the last non-numeric character
                var lastNonNumericIndex = 0;
                for(var i = 0; i < nextSerialNumber.Length; i++) {

                    var charCode = (int)nextSerialNumber[i];
                    if (charCode < 48 || charCode > 57)
                        lastNonNumericIndex = i;
                }
                // parse the remainder as an integer
                var numericLabel = nextSerialNumber.Substring(lastNonNumericIndex + 1, nextSerialNumber.Length - (lastNonNumericIndex + 1));
                if (int.TryParse(numericLabel, out var parsedNumber))
                {
                    // increment it
                    var nextSerialNumberInt = parsedNumber + 1;
                    var labelPortion = nextSerialNumber.Substring(0, lastNonNumericIndex + 1);
                    serialNumber = labelPortion.PadRight(labelPortion.Length + numericLabel.Length - nextSerialNumberInt.ToString().Length, '0') + nextSerialNumberInt;
                    return serialNumber;
                }
            }

            return serialNumber;
        }
    }
}
