// Helper Functions
function formatHour(date, timezone = "UTC", showMinutes = false) {
  const options = {
    hour: "2-digit",
    minute: showMinutes ? "2-digit" : undefined,
    hour12: false,
    timeZone: timezone,
  };
  return date.toLocaleTimeString("en-US", options);
}

function calculateTimePosition(date, startTime, hourWidth) {
  const diff = (date.getTime() - startTime.getTime()) / (1000 * 60 * 60);

  if (diff < 0) {
    console.warn("Date is before timeline start:", {
      date: date.toISOString(),
      startTime: startTime.toISOString(),
    });
    return 0;
  }

  return diff;
}
function createTooltipContent(type, data) {
  const templates = {
    assignment: ({
      id,
      startDate,
      endDate,
      timezone,
      isOptimal,
      eligibleTechs,
      techIds,
      status,
    }) => `
                <div style="margin-bottom: 5px; font-weight: bold; color: #42a5f5;">
                    Task ID: ${id}
                </div>
                <div style="margin-bottom: 5px; color: #90caf9;">
                    Time: ${formatHour(startDate, timezone, true)} - ${formatHour(endDate, timezone, true)} (${timezone})
                </div>
                <div style="margin-bottom: 5px; color: ${status === "pre_assigned" ? "#ffc107" : isOptimal ? "#66bb6a" : "#90caf9"};">
                    ${status === "pre_assigned" ? "Pre-Assigned O" : isOptimal ? "Optimal Assignment &#10003;" : "Sub-optimal Assignment"}
                </div>
                <table>
                    <tr>
                        <th>Tech ID</th>
                        <th>Score</th>
                        <th>Assigned</th>
                    </tr>
                    ${eligibleTechs
            .sort((a, b) => b.score - a.score)
            .map(
              (tech) => `
                            <tr>
                                <td>${tech.id.substring(0, 8)}</td>
                                <td>${tech.score.toFixed(2)}</td>
                                <td>${techIds.includes(tech.id) ? "&#10003" : ""}</td>
                            </tr>
                        `,
            )
            .join("")}
                </table>
            `,

    nonAvailability: ({ startTime, endTime, timezone }) => `
            <div style="color: #ef9a9a;">Non-Available Time</div>
            <div style="margin-top: 5px;">
                ${formatHour(startTime, timezone, true)} - ${formatHour(endTime, timezone, true)}
            </div>
        `,
    unassigned: ({ priority, appointment, timezone, eligibleTechs }) => `
            <div style="color: #ef9a9a; margin-bottom: 5px;">Unassigned Job Details</div>
            <div style="margin-bottom: 5px;">
                Priority: ${priority}
                ${appointment.arrival_window.start
        ? `<br>Window: ${formatHour(new Date(appointment.arrival_window.start), timezone, true)} -
                     ${formatHour(new Date(appointment.arrival_window.end), timezone, true)}`
        : "<br>No Time Window"
      }
            </div>
            <table>
                <tr>
                    <th>Tech ID</th>
                    <th>Score</th>
                </tr>
                ${eligibleTechs
        .sort((a, b) => b.score - a.score)
        .map(
          (tech) => `
                        <tr>
                            <td>${tech.id.substring(0, 8)}</td>
                            <td>${tech.score.toFixed(2)}</td>
                        </tr>
                    `,
        )
        .join("")}
            </table>
        `,
  };
  return templates[type](data);
}

function attachTooltipListeners(element, tooltipContent, tooltip) {
  element.addEventListener("mouseenter", () => {
    tooltip.innerHTML = tooltipContent;
    tooltip.style.display = "block";
  });

  element.addEventListener("mouseleave", () => {
    tooltip.style.display = "none";
  });
}

function createBox(className, content, left, width) {
  const box = document.createElement("div");
  box.className = className;
  box.style.left = `${Math.round(left)}px`;
  box.style.width = `${Math.round(width)}px`;
  box.innerHTML = content;
  return box;
}
