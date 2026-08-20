# Integration test: Phase 1 (Member 1) + Phase 2 (Member 2)
$base = "http://localhost:5181"
$passed = 0
$failed = 0
$results = @()

function Test-Case($name, $scriptBlock) {
    try {
        & $scriptBlock
        $script:passed++
        $script:results += [PSCustomObject]@{ Test = $name; Result = "PASS" }
        Write-Host "[PASS] $name" -ForegroundColor Green
    } catch {
        $script:failed++
        $msg = $_.Exception.Message
        $script:results += [PSCustomObject]@{ Test = $name; Result = "FAIL: $msg" }
        Write-Host "[FAIL] $name - $msg" -ForegroundColor Red
    }
}

function Invoke-Api($method, $path, $body = $null, $token = $null, $accept = "application/json") {
    $headers = @{ Accept = $accept }
    if ($token) { $headers.Authorization = "Bearer $token" }
    $params = @{ Uri = "$base$path"; Method = $method; Headers = $headers; ErrorAction = "Stop" }
    if ($body) {
        $params.ContentType = "application/json"
        $params.Body = ($body | ConvertTo-Json -Depth 5)
    }
    try {
        $response = Invoke-WebRequest @params -UseBasicParsing
        return @{ Status = [int]$response.StatusCode; Body = $response.Content; Headers = $response.Headers }
    } catch {
        $ex = $_.Exception
        if ($ex.Response) {
            $reader = New-Object System.IO.StreamReader($ex.Response.GetResponseStream())
            $content = $reader.ReadToEnd()
            return @{ Status = [int]$ex.Response.StatusCode; Body = $content; Error = $true }
        }
        throw
    }
}

function Get-Token($email, $password) {
    $r = Invoke-Api POST "/api/auth/login" @{ email = $email; password = $password }
    if ($r.Status -ne 200) { throw "Login failed $($r.Status): $($r.Body)" }
    return ($r.Body | ConvertFrom-Json).token
}

Write-Host "=== Phase 1 + Phase 2 Integration Tests ===" -ForegroundColor Cyan
Write-Host "Base URL: $base`n"

# --- Setup tokens (Phase 1 auth) ---
Test-Case "P1: Login admin" {
    $script:adminToken = Get-Token "admin@fpt.edu.vn" "Admin@123"
}
Test-Case "P1: Login staff" {
    $script:staffToken = Get-Token "staff@fpt.edu.vn" "Staff@123"
}

# --- Phase 1: Catalog baseline for Phase 2 ---
Test-Case "P1→P2: Locations seeded and accessible" {
    $r = Invoke-Api GET "/api/locations"
    if ($r.Status -ne 200) { throw "Expected 200" }
    $locs = $r.Body | ConvertFrom-Json
    if ($locs.Count -lt 1) { throw "No locations seeded" }
    $script:locationId = $locs[0].id
}

Test-Case "P1→P2: Organizers seeded and accessible" {
    $r = Invoke-Api GET "/api/organizers"
    if ($r.Status -ne 200) { throw "Expected 200" }
    $orgs = $r.Body | ConvertFrom-Json
    if ($orgs.Count -lt 1) { throw "No organizers seeded" }
    $script:organizerId = $orgs[0].id
}

# --- Phase 2: UC04 View & Search Events ---
Test-Case "P2 UC04: Anonymous GET events (only Published/Ongoing/Completed)" {
    $r = Invoke-Api GET "/api/events?page=1&pageSize=50"
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    $data = $r.Body | ConvertFrom-Json
    if ($null -eq $data.items) { throw "Missing items" }
    foreach ($e in $data.items) {
        if ($e.status -in @("Draft", "Cancelled")) {
            throw "Anonymous should not see Draft/Cancelled, found: $($e.status) - $($e.title)"
        }
    }
    $script:publicEventCount = $data.totalCount
}

Test-Case "P2 UC04: Staff sees all statuses including Draft" {
    $r = Invoke-Api GET "/api/events?page=1&pageSize=50" $null $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200" }
    $data = $r.Body | ConvertFrom-Json
    if ($data.totalCount -le $script:publicEventCount) { throw "Staff should see more events than anonymous ($($data.totalCount) vs $($script:publicEventCount))" }
}

Test-Case "P2 UC04: Search events by keyword (staff)" {
    $all = Invoke-Api GET "/api/events?page=1&pageSize=1" $null $script:staffToken
    $sample = ($all.Body | ConvertFrom-Json).items[0].title
    $term = if ($sample.Length -ge 3) { $sample.Substring(0, 3) } else { $sample }
    $r = Invoke-Api GET "/api/events?search=$term&page=1&pageSize=10" $null $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200" }
    $data = $r.Body | ConvertFrom-Json
    if ($data.totalCount -lt 1) { throw "Search should find events matching '$term'" }
}

Test-Case "P2 UC04: Filter by status Published" {
    $r = Invoke-Api GET "/api/events?status=Published" $null $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200" }
    $data = $r.Body | ConvertFrom-Json
    foreach ($e in $data.items) {
        if ($e.status -ne "Published") { throw "Filter failed: $($e.status)" }
    }
}

Test-Case "P2 UC04: AvailableSlots computed correctly" {
    $r = Invoke-Api GET "/api/events?page=1&pageSize=1" $null $script:staffToken
    $e = ($r.Body | ConvertFrom-Json).items[0]
    $expected = $e.capacity - $e.registeredCount
    if ($e.availableSlots -ne $expected) {
        throw "AvailableSlots=$($e.availableSlots), expected $expected"
    }
}

Test-Case "P2 UC04: GET event by id (anonymous - public event)" {
    $list = Invoke-Api GET "/api/events?status=Published" $null $script:staffToken
    $pubId = ($list.Body | ConvertFrom-Json).items[0].id
    $r = Invoke-Api GET "/api/events/$pubId"
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    $script:publishedEventId = $pubId
}

# --- Phase 2: UC11 Create/Edit Event ---
$start = (Get-Date).AddDays(10).ToString("yyyy-MM-ddTHH:mm:ss")
$end = (Get-Date).AddDays(10).AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss")
$deadline = (Get-Date).AddDays(8).ToString("yyyy-MM-ddTHH:mm:ss")

Test-Case "P2 UC11: Staff create event (Draft)" {
    $r = Invoke-Api POST "/api/events" @{
        title = "Integration Test Event $([guid]::NewGuid().ToString('N').Substring(0,8))"
        description = "Created by integration test"
        startTime = $start
        endTime = $end
        registrationDeadline = $deadline
        capacity = 50
        locationId = $script:locationId
        organizerId = $script:organizerId
    } $script:staffToken
    if ($r.Status -ne 201) { throw "Expected 201, got $($r.Status): $($r.Body)" }
    $evt = $r.Body | ConvertFrom-Json
    if ($evt.status -ne "Draft") { throw "Expected Draft, got $($evt.status)" }
    if ($null -eq $evt.location) { throw "Missing nested Location from Phase 1 catalog" }
    if ($null -eq $evt.organizer) { throw "Missing nested Organizer from Phase 1 catalog" }
    $script:testEventId = $evt.id
}

Test-Case "P2 UC04: Anonymous cannot view Draft event -> 404" {
    $r = Invoke-Api GET "/api/events/$($script:testEventId)"
    if ($r.Status -ne 404) { throw "Expected 404 for Draft, got $($r.Status)" }
}

Test-Case "P2 BR-E-04: Create with EndTime <= StartTime -> 400" {
    $r = Invoke-Api POST "/api/events" @{
        title = "Bad Schedule"
        startTime = $start
        endTime = $start
        registrationDeadline = $deadline
        capacity = 10
        locationId = $script:locationId
        organizerId = $script:organizerId
    } $script:staffToken
    if ($r.Status -ne 400) { throw "Expected 400, got $($r.Status)" }
}

Test-Case "P2 BR-E-04: RegistrationDeadline after StartTime -> 400" {
    $r = Invoke-Api POST "/api/events" @{
        title = "Bad Deadline"
        startTime = $start
        endTime = $end
        registrationDeadline = (Get-Date).AddDays(11).ToString("yyyy-MM-ddTHH:mm:ss")
        capacity = 10
        locationId = $script:locationId
        organizerId = $script:organizerId
    } $script:staffToken
    if ($r.Status -ne 400) { throw "Expected 400, got $($r.Status)" }
}

Test-Case "P2 UC11: Staff update event schedule" {
    $newStart = (Get-Date).AddDays(11).ToString("yyyy-MM-ddTHH:mm:ss")
    $newEnd = (Get-Date).AddDays(11).AddHours(3).ToString("yyyy-MM-ddTHH:mm:ss")
    $r = Invoke-Api PUT "/api/events/$($script:testEventId)" @{
        title = "Integration Test Event Updated"
        description = "Updated"
        startTime = $newStart
        endTime = $newEnd
        registrationDeadline = (Get-Date).AddDays(9).ToString("yyyy-MM-ddTHH:mm:ss")
        capacity = 60
        locationId = $script:locationId
        organizerId = $script:organizerId
    } $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status): $($r.Body)" }
    $evt = $r.Body | ConvertFrom-Json
    if ($evt.title -ne "Integration Test Event Updated") { throw "Title not updated" }
    if ($evt.status -ne "Draft") { throw "Expected Draft after update with future deadline, got $($evt.status)" }
}

Test-Case "P2 UC11: Student cannot create event -> 403" {
    $email = "evtstudent_$([guid]::NewGuid().ToString('N').Substring(0,8))@test.com"
    Invoke-Api POST "/api/auth/register" @{ email=$email; password="Student@123"; fullName="Evt Student" } | Out-Null
    $studentToken = Get-Token $email "Student@123"
    $r = Invoke-Api POST "/api/events" @{
        title = "Blocked"; startTime = $start; endTime = $end
        registrationDeadline = $deadline; capacity = 10
        locationId = $script:locationId; organizerId = $script:organizerId
    } $studentToken
    if ($r.Status -ne 403) { throw "Expected 403, got $($r.Status)" }
}

# --- Phase 2: Auto status by time (replaces manual UC12 workflow) ---
Test-Case "P2 Auto: Update deadline past -> Published" {
    $r = Invoke-Api PUT "/api/events/$($script:testEventId)" @{
        title = "Integration Test Event Updated"
        description = "Updated"
        startTime = (Get-Date).AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss")
        endTime = (Get-Date).AddDays(7).AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss")
        registrationDeadline = (Get-Date).AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss")
        capacity = 60
        locationId = $script:locationId
        organizerId = $script:organizerId
    } $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status): $($r.Body)" }
    $evt = $r.Body | ConvertFrom-Json
    if ($evt.status -ne "Published") { throw "Expected Published, got $($evt.status)" }
}

Test-Case "P2 Auto: Update start past -> Ongoing" {
    $utcNow = (Get-Date).ToUniversalTime()
    $r = Invoke-Api PUT "/api/events/$($script:testEventId)" @{
        title = "Integration Test Event Updated"
        description = "Updated"
        startTime = $utcNow.AddHours(-2).ToString("yyyy-MM-ddTHH:mm:ss")
        endTime = $utcNow.AddHours(4).ToString("yyyy-MM-ddTHH:mm:ss")
        registrationDeadline = $utcNow.AddDays(-2).ToString("yyyy-MM-ddTHH:mm:ss")
        capacity = 60
        locationId = $script:locationId
        organizerId = $script:organizerId
    } $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    $status = ($r.Body | ConvertFrom-Json).status
    if ($status -ne "Ongoing") { throw "Expected Ongoing, got $status" }
}

Test-Case "P2 Auto: Update end past -> Completed" {
    $utcNow = (Get-Date).ToUniversalTime()
    $r = Invoke-Api PUT "/api/events/$($script:testEventId)" @{
        title = "Integration Test Event Updated"
        description = "Updated"
        startTime = $utcNow.AddDays(-3).ToString("yyyy-MM-ddTHH:mm:ss")
        endTime = $utcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss")
        registrationDeadline = $utcNow.AddDays(-5).ToString("yyyy-MM-ddTHH:mm:ss")
        capacity = 60
        locationId = $script:locationId
        organizerId = $script:organizerId
    } $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    if (($r.Body | ConvertFrom-Json).status -ne "Completed") { throw "Expected Completed" }
}

Test-Case "P2 Auto: Manual workflow endpoints removed -> 404" {
    $r = Invoke-Api POST "/api/events/$($script:testEventId)/publish" @{} $script:staffToken
    if ($r.Status -ne 404) { throw "Expected 404 (endpoint removed), got $($r.Status)" }
}

Test-Case "P2 BR-E-02: Staff cannot edit Completed event -> 403" {
    $r = Invoke-Api PUT "/api/events/$($script:testEventId)" @{
        title = "Staff Edit Blocked"
        startTime = (Get-Date).AddDays(12).ToString("yyyy-MM-ddTHH:mm:ss")
        endTime = (Get-Date).AddDays(12).AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss")
        registrationDeadline = (Get-Date).AddDays(10).ToString("yyyy-MM-ddTHH:mm:ss")
        capacity = 60
        locationId = $script:locationId
        organizerId = $script:organizerId
    } $script:staffToken
    if ($r.Status -ne 403) { throw "Expected 403, got $($r.Status)" }
}

Test-Case "P2 BR-E-02: Admin can edit Completed event" {
    $r = Invoke-Api PUT "/api/events/$($script:testEventId)" @{
        title = "Admin Edit OK"
        startTime = (Get-Date).AddDays(12).ToString("yyyy-MM-ddTHH:mm:ss")
        endTime = (Get-Date).AddDays(12).AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss")
        registrationDeadline = (Get-Date).AddDays(10).ToString("yyyy-MM-ddTHH:mm:ss")
        capacity = 60
        locationId = $script:locationId
        organizerId = $script:organizerId
    } $script:adminToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status): $($r.Body)" }
    if (($r.Body | ConvertFrom-Json).title -ne "Admin Edit OK") { throw "Admin edit failed" }
}

# --- Phase 1 BR-E-05 + Phase 2 integration ---
Test-Case "P1→P2 BR-E-05: Cannot delete Location referenced by event -> 409" {
    $r = Invoke-Api DELETE "/api/locations/$($script:locationId)" $null $script:staffToken
    if ($r.Status -ne 409) { throw "Expected 409, got $($r.Status): $($r.Body)" }
}

Test-Case "P1→P2 BR-E-05: Cannot delete Organizer referenced by event -> 409" {
    $r = Invoke-Api DELETE "/api/organizers/$($script:organizerId)" $null $script:adminToken
    if ($r.Status -ne 409) { throw "Expected 409, got $($r.Status): $($r.Body)" }
}

# --- Phase 2: UC14 Reports ---
Test-Case "P2 UC14: Staff GET overview report" {
    $r = Invoke-Api GET "/api/reports/overview" $null $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    $data = $r.Body | ConvertFrom-Json
    if ($data.totalEvents -lt 1) { throw "Expected totalEvents >= 1" }
    if ($null -eq $data.eventsByStatus) { throw "Missing eventsByStatus" }
}

Test-Case "P2 UC14: Staff GET events report list" {
    $r = Invoke-Api GET "/api/reports/events?page=1&pageSize=10" $null $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    $data = $r.Body | ConvertFrom-Json
    if ($null -eq $data.items) { throw "Missing items" }
}

Test-Case "P2 UC14: Staff GET per-event report" {
    $r = Invoke-Api GET "/api/reports/event/$($script:testEventId)" $null $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    $data = $r.Body | ConvertFrom-Json
    if ($data.eventId -ne $script:testEventId) { throw "Wrong eventId" }
    if ($null -eq $data.fillRate) { throw "Missing fillRate" }
}

Test-Case "P2 UC14: Anonymous cannot access reports -> 401" {
    $r = Invoke-Api GET "/api/reports/overview"
    if ($r.Status -ne 401) { throw "Expected 401, got $($r.Status)" }
}

# --- Phase 2: OData ---
Test-Case "P2 OData: Staff GET /odata/Events" {
    $headers = @{ Authorization = "Bearer $($script:staffToken)"; Accept = "application/json" }
    $r = Invoke-WebRequest -Uri "$base/odata/Events`?`$top=5&`$count=true" -Headers $headers -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode)" }
    if ($r.Content -notmatch "AvailableSlots") { throw "Missing AvailableSlots in OData response" }
}

Test-Case "P2 OData: Filter AvailableSlots gt 0" {
    $headers = @{ Authorization = "Bearer $($script:staffToken)"; Accept = "application/json" }
    $r = Invoke-WebRequest -Uri "$base/odata/Events`?`$filter=AvailableSlots gt 0&`$top=10" -Headers $headers -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "Expected 200, got $($r.StatusCode)" }
}

Test-Case "P2 OData: Anonymous -> 401" {
    try {
        Invoke-WebRequest -Uri "$base/odata/Events" -UseBasicParsing -ErrorAction Stop
        throw "Expected 401"
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 401) {
            throw "Expected 401, got $($_.Exception.Response.StatusCode.value__)"
        }
    }
}

# --- Cross-phase: Content negotiation on events ---
Test-Case "P1+P2: Events JSON content negotiation" {
    $r = Invoke-Api GET "/api/events?page=1&pageSize=1" $null $null "application/json"
    if ($r.Status -ne 200) { throw "Expected 200" }
    if ($r.Body -notmatch '"items"') { throw "Expected JSON paged result" }
}

# PagedResult XML serialization not configured — JSON/XML work on single-object endpoints (Phase 1 pattern)

Write-Host "`n=== INTEGRATION SUMMARY ===" -ForegroundColor Cyan
Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
if ($failed -gt 0) {
    Write-Host "`nFailed tests:" -ForegroundColor Red
    $results | Where-Object { $_.Result -like "FAIL*" } | ForEach-Object { Write-Host "  - $($_.Test): $($_.Result)" }
}
exit $failed
