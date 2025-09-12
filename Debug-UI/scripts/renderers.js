// Render Functions
function renderTimelineHeader(timeline, startTime, totalHours, hourWidth) {
  const hourMarkers = document.createElement("div");
  hourMarkers.className = "hour-markers";

  for (let i = 0; i <= totalHours; i++) {
    const marker = document.createElement("div");
    marker.className = "hour-marker";
    marker.style.left = `${i * hourWidth}px`;

    const label = document.createElement("div");
    label.className = "hour-label";
    const markerTime = new Date(startTime.getTime());
    markerTime.setUTCHours(startTime.getUTCHours() + i, 0, 0, 0);
    label.textContent = formatHour(
      markerTime,
      document.getElementById("timezone").value,
    );

    marker.appendChild(label);
    hourMarkers.appendChild(marker);
  }

  timeline.appendChild(hourMarkers);
}

function renderWorkTime(row, tech, startTime, hourWidth) {
  if (!tech.work_time) return;

  const workStartTime = new Date(tech.work_time.start);
  const workEndTime = new Date(tech.work_time.end);

  const left =
    calculateTimePosition(workStartTime, startTime, hourWidth) * hourWidth;
  const width =
    ((workEndTime.getTime() - workStartTime.getTime()) / (1000 * 60 * 60)) *
    hourWidth;

  const workTimeIndicator = document.createElement("div");
  workTimeIndicator.className = "work-time-indicator";
  workTimeIndicator.style.left = `${Math.max(0, Math.round(left))}px`;
  workTimeIndicator.style.width = `${Math.round(width)}px`;

  row.appendChild(workTimeIndicator);
}

function renderNonAvailabilities(
  row,
  tech,
  response,
  startTime,
  hourWidth,
  tooltip,
) {
  if (!response.nonavailibilities) return;

  const nonAvailability = response.nonavailibilities.find(
    (na) => na.technician_id === tech.id,
  );

  if (
    !nonAvailability?.non_availabilities ||
    nonAvailability.non_availabilities.length === 0
  )
    return;

  nonAvailability.non_availabilities.forEach((na) => {
    if (!na.start || !na.finish) return;

    const naStartTime = new Date(na.start);
    const naEndTime = new Date(na.finish);

    if (isNaN(naStartTime.getTime()) || isNaN(naEndTime.getTime())) return;

    const left =
      calculateTimePosition(naStartTime, startTime, hourWidth) * hourWidth;
    const width =
      ((naEndTime.getTime() - naStartTime.getTime()) / (1000 * 60 * 60)) *
      hourWidth;

    // Non-availability göstergesi oluştur
    const naIndicator = document.createElement("div");
    naIndicator.className = "non-availability-indicator";
    naIndicator.style.left = `${Math.max(0, Math.round(left))}px`;
    naIndicator.style.width = `${Math.round(width)}px`;

    // Non-availability label'ını oluştur
    const naLabel = document.createElement("div");
    naLabel.className = "non-availability-label";
    naLabel.textContent = "Non-Availability";

    naIndicator.appendChild(naLabel);

    // Tooltip içeriğini güncelle
    const tooltipContent = `
      <strong>Non-Availability Period</strong><br/>
      Start: ${formatHour(naStartTime, document.getElementById("timezone").value, true)}<br/>
      End: ${formatHour(naEndTime, document.getElementById("timezone").value, true)}
    `;

    naIndicator.addEventListener("mouseenter", () => {
      tooltip.style.display = "block";
      tooltip.innerHTML = tooltipContent;
    });

    naIndicator.addEventListener("mouseleave", () => {
      tooltip.style.display = "none";
    });

    row.appendChild(naIndicator);
  });
}

function renderAssignments(
  row,
  tech,
  response,
  request,
  startTime,
  hourWidth,
  tooltip,
) {
  const assignments = response.assignments.filter(
    (a) =>
      a.technician_ids &&
      a.technician_ids.includes(tech.id) &&
      ["assigned", "pre_assigned"].includes(a.status),
  );

  assignments.forEach((assignment) => {
    const request_appointment = request.appointments.find(
      (a) => a.id === assignment.id,
    );
    if (!request_appointment) return;

    const startDate = new Date(assignment.start);
    const endDate = new Date(assignment.end);

    const left = calculateTimePosition(startDate, startTime, hourWidth);
    const width = Math.max(
      ((endDate.getTime() - startDate.getTime()) / (1000 * 60 * 60)) *
        hourWidth,
      CONSTANTS.HOUR_WIDTH / 4,
    );

    const eligibleTechs = request_appointment.eligible_technicians;
    const highestScoringTech = [...eligibleTechs].sort(
      (a, b) => b.score - a.score,
    )[0];

    // Status kontrolü
    const isPreAssigned = assignment.status === "pre_assigned";
    const isOptimalAssignment =
      !isPreAssigned &&
      assignment.technician_ids.includes(highestScoringTech.id);

    // CSS class'ını belirle
    let boxClass = "time-box";
    if (isPreAssigned) {
      boxClass += " pre-assigned";
    } else if (isOptimalAssignment) {
      boxClass += " optimal";
    }

    const arrivalWindow = request_appointment.arrival_window;
    const arrivalWindowStart = new Date(arrivalWindow.start);
    const arrivalWindowEnd = new Date(arrivalWindow.end);

    const assignmentBox = createBox(
      boxClass,
      `
      P: <strong>${request_appointment.priority}</strong> - R: <strong>${eligibleTechs.findIndex((t) => assignment.technician_ids.includes(t.id)) + 1}</strong> - D: <strong>${assignment.route.distance.toLocaleString("en-US", { maximumFractionDigits: 2 })}</strong><br/>
      ${formatHour(arrivalWindowStart, document.getElementById("timezone").value, true)} - ${formatHour(arrivalWindowEnd, document.getElementById("timezone").value, true)}
      `,
      Math.round(left * hourWidth),
      Math.round(width),
    );

    const tooltipContent = createTooltipContent("assignment", {
      id: assignment.id,
      startDate,
      endDate,
      timezone: document.getElementById("timezone").value,
      isOptimal: isOptimalAssignment,
      eligibleTechs,
      techIds: assignment.technician_ids,
      status: assignment.status,
    });

    attachTooltipListeners(assignmentBox, tooltipContent, tooltip);
    row.appendChild(assignmentBox);
  });
}

function renderUnassignedJobs(unassignedSection, response, request, tooltip) {
  const unassignedHeader = document.createElement("div");
  unassignedHeader.className = "unassigned-header";
  unassignedHeader.textContent = "Unassigned Jobs";
  unassignedSection.appendChild(unassignedHeader);

  const unassignedJobs = response.assignments
    .filter(
      (a) =>
        a.status === "outlier" ||
        !a.technician_ids ||
        a.technician_ids.length === 0,
    )
    .sort((a, b) => {
      const appA = request.appointments.find((app) => app.id === a.id);
      const appB = request.appointments.find((app) => app.id === b.id);
      return (appB?.priority || 0) - (appA?.priority || 0);
    });

  unassignedJobs.forEach((job) => {
    const request_appointment = request.appointments.find(
      (a) => a.id === job.id,
    );
    if (!request_appointment) return;

    const box = document.createElement("div");
    box.className = "unassigned-box";

    const location = request_appointment.location.coordinate.split(",");
    box.innerHTML = `
                <div class="priority-badge">
                    Priority: ${request_appointment.priority}
                </div>
                <div>ID: ${job.id}</div>
                <div>Location: ${parseFloat(location[1]).toFixed(4)}, ${parseFloat(location[0]).toFixed(4)}</div>
                <div style="color: #d32f2f; margin-top: 4px;">Status: ${job.status}</div>
            `;

    const tooltipContent = createTooltipContent("unassigned", {
      priority: request_appointment.priority,
      appointment: request_appointment,
      timezone: document.getElementById("timezone").value,
      eligibleTechs: request_appointment.eligible_technicians,
    });

    attachTooltipListeners(box, tooltipContent, tooltip);
    unassignedSection.appendChild(box);
  });
}

function processData() {
  try {
    const requestData = requestEditor.get();
    const responseData = responseEditor.get();
    renderBoard(requestData, responseData);
  } catch (error) {
    console.error("JSON işlenirken hata oluştu:", error);
  }
}

function renderBoard(request, response) {
  const timeline = document.getElementById("timeline");
  const techList = document.getElementById("techList");
  const unassignedSection = document.getElementById("unassigned");

  // Clear previous content
  timeline.innerHTML = "";
  techList.innerHTML = "";
  unassignedSection.innerHTML = "";

  // Planlama ufkunun başlangıç zamanını tüm atamalar içindeki en erken zamana göre ayarlayalım
  const allDates = response.assignments
    .filter((a) => a.start && a.end)
    .flatMap((a) => [new Date(a.start), new Date(a.end)]);

  // Teknisyenlerin çalışma zamanlarını da ekleyelim
  request.technicians.forEach((tech) => {
    if (tech.work_time) {
      allDates.push(new Date(tech.work_time.start));
      allDates.push(new Date(tech.work_time.end));
    }
  });

  const startTime = new Date(Math.min(...allDates.map((d) => d.getTime())));
  const endTime = new Date(Math.max(...allDates.map((d) => d.getTime())));

  const totalHours = Math.ceil((endTime - startTime) / (1000 * 60 * 60));
  const timelineWidth = (totalHours + 2) * CONSTANTS.HOUR_WIDTH;

  // Create tooltip
  const tooltip = document.createElement("div");
  tooltip.className = "tooltip";
  document.body.appendChild(tooltip);

  document.addEventListener("mousemove", (e) => {
    tooltip.style.left = `${e.pageX + 10}px`;
    tooltip.style.top = `${e.pageY + 10}px`;
  });

  // Render timeline components
  renderTimelineHeader(timeline, startTime, totalHours, CONSTANTS.HOUR_WIDTH);

  // Add header row to techList (boş bir header row ekleyerek aşağı kaydırıyoruz)
  const headerRow = document.createElement("div");
  headerRow.className = "tech-name header";
  techList.appendChild(headerRow);
  // Render technician rows
  request.technicians.forEach((tech) => {
    const techName = document.createElement("div");
    techName.className = "tech-name";
    techName.textContent = tech.id.substring(0, 8);
    techList.appendChild(techName);

    const row = document.createElement("div");
    row.className = "tech-row";
    timeline.appendChild(row);

    renderWorkTime(row, tech, startTime, CONSTANTS.HOUR_WIDTH);
    renderNonAvailabilities(
      row,
      tech,
      response,
      startTime,
      CONSTANTS.HOUR_WIDTH,
      tooltip,
    );
    renderAssignments(
      row,
      tech,
      response,
      request,
      startTime,
      CONSTANTS.HOUR_WIDTH,
      tooltip,
    );
  });

  renderUnassignedJobs(unassignedSection, response, request, tooltip);

  timeline.style.width = `${timelineWidth}px`;
}
