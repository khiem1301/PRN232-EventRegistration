# EventApi automated test script
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

Write-Host "=== EventApi Automated Tests ===" -ForegroundColor Cyan
Write-Host "Base URL: $base`n"

# UC01
Test-Case "UC01 Register new student" {
    $email = "autotest_$([guid]::NewGuid().ToString('N').Substring(0,8))@test.com"
    $r = Invoke-Api POST "/api/auth/register" @{ email=$email; password="Student@123"; fullName="Auto Test"; studentCode="SE999001"; phone="0909999999" }
    if ($r.Status -ne 201) { throw "Expected 201, got $($r.Status): $($r.Body)" }
    $script:testStudentEmail = $email
}

Test-Case "UC01 Register duplicate email -> 409" {
    $r = Invoke-Api POST "/api/auth/register" @{ email=$script:testStudentEmail; password="Student@123"; fullName="Dup"; studentCode="SE999002" }
    if ($r.Status -ne 409) { throw "Expected 409, got $($r.Status)" }
}

# UC02
Test-Case "UC02 Login admin success" {
    $script:adminToken = Get-Token "admin@fpt.edu.vn" "Admin@123"
    if (-not $script:adminToken) { throw "No token returned" }
}

Test-Case "UC02 Login staff success" {
    $script:staffToken = Get-Token "staff@fpt.edu.vn" "Staff@123"
}

Test-Case "UC02 Login student success" {
    $script:studentToken = Get-Token $script:testStudentEmail "Student@123"
}

Test-Case "UC02 Wrong password -> 401" {
    $r = Invoke-Api POST "/api/auth/login" @{ email="admin@fpt.edu.vn"; password="WrongPass" }
    if ($r.Status -ne 401) { throw "Expected 401, got $($r.Status)" }
}

Test-Case "UC02 Inactive user -> 403" {
    # Create temp user, deactivate, try login
    $email = "inactive_$([guid]::NewGuid().ToString('N').Substring(0,8))@test.com"
    $reg = Invoke-Api POST "/api/auth/register" @{ email=$email; password="Student@123"; fullName="Inactive User" }
    $user = $reg.Body | ConvertFrom-Json
    Invoke-Api DELETE "/api/users/$($user.id)" $null $script:adminToken | Out-Null
    $r = Invoke-Api POST "/api/auth/login" @{ email=$email; password="Student@123" }
    if ($r.Status -ne 403) { throw "Expected 403, got $($r.Status): $($r.Body)" }
}

# UC03
Test-Case "UC03 GET /account/me" {
    $r = Invoke-Api GET "/api/account/me" $null $script:studentToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    $me = $r.Body | ConvertFrom-Json
    if ($me.email -ne $script:testStudentEmail) { throw "Wrong email in profile" }
}

Test-Case "UC03 PUT /account/me" {
    $r = Invoke-Api PUT "/api/account/me" @{ fullName="Updated Name"; studentCode="SE888888"; phone="0908888888" } $script:studentToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    $me = $r.Body | ConvertFrom-Json
    if ($me.fullName -ne "Updated Name") { throw "Profile not updated" }
}

Test-Case "UC03 Change password wrong current -> 400" {
    $r = Invoke-Api PUT "/api/account/me/password" @{ currentPassword="WrongOld"; newPassword="NewPass@123" } $script:studentToken
    if ($r.Status -ne 400) { throw "Expected 400, got $($r.Status)" }
}

Test-Case "UC03 Change password success" {
    $r = Invoke-Api PUT "/api/account/me/password" @{ currentPassword="Student@123"; newPassword="NewPass@456" } $script:studentToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    $script:studentToken = Get-Token $script:testStudentEmail "NewPass@456"
}

Test-Case "UC03 Unauthorized without token -> 401" {
    $r = Invoke-Api GET "/api/account/me"
    if ($r.Status -ne 401) { throw "Expected 401, got $($r.Status)" }
}

# UC09
Test-Case "UC09 Admin list users" {
    $r = Invoke-Api GET "/api/users?page=1&pageSize=5" $null $script:adminToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    $data = $r.Body | ConvertFrom-Json
    if ($null -eq $data.items) { throw "Missing items in paged result" }
}

Test-Case "UC09 Admin search users" {
    $r = Invoke-Api GET "/api/users?search=admin&page=1&pageSize=10" $null $script:adminToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
}

Test-Case "UC09 Admin create user" {
    $email = "staffnew_$([guid]::NewGuid().ToString('N').Substring(0,8))@test.com"
    $r = Invoke-Api POST "/api/users" @{ email=$email; password="Staff@123"; fullName="New Staff"; role="Staff" } $script:adminToken
    if ($r.Status -ne 201) { throw "Expected 201, got $($r.Status): $($r.Body)" }
    $script:newStaffId = ($r.Body | ConvertFrom-Json).id
}

Test-Case "UC09 Admin update user" {
    $r = Invoke-Api PUT "/api/users/$($script:newStaffId)" @{ fullName="Updated Staff"; role="Staff"; isActive=$true } $script:adminToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
}

Test-Case "UC09 Staff cannot access users -> 403" {
    $r = Invoke-Api GET "/api/users" $null $script:staffToken
    if ($r.Status -ne 403) { throw "Expected 403, got $($r.Status)" }
}

Test-Case "UC09 Admin deactivate user" {
    $r = Invoke-Api DELETE "/api/users/$($script:newStaffId)" $null $script:adminToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
}

# UC10 Locations
Test-Case "UC10 GET locations anonymous" {
    $r = Invoke-Api GET "/api/locations"
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
}

Test-Case "UC10 Staff create location" {
    $r = Invoke-Api POST "/api/locations" @{ name="Test Hall"; address="Test Address"; description="Auto test"; capacity=100 } $script:staffToken
    if ($r.Status -ne 201) { throw "Expected 201, got $($r.Status): $($r.Body)" }
    $script:testLocationId = ($r.Body | ConvertFrom-Json).id
}

Test-Case "UC10 Create location without capacity defaults to 100" {
    $r = Invoke-Api POST "/api/locations" @{ name="Default Cap Hall"; address="Test Address" } $script:staffToken
    if ($r.Status -ne 201) { throw "Expected 201, got $($r.Status): $($r.Body)" }
    $loc = $r.Body | ConvertFrom-Json
    if ($loc.capacity -ne 100) { throw "Expected default capacity 100, got $($loc.capacity)" }
    Invoke-Api DELETE "/api/locations/$($loc.id)" $null $script:staffToken | Out-Null
}

Test-Case "UC10 Create location invalid capacity -> 400" {
    $r = Invoke-Api POST "/api/locations" @{ name="Bad Cap"; address="Addr"; capacity=0 } $script:staffToken
    if ($r.Status -ne 400) { throw "Expected 400, got $($r.Status)" }
}

Test-Case "UC10 Staff update location" {
    $r = Invoke-Api PUT "/api/locations/$($script:testLocationId)" @{ name="Test Hall Updated"; address="New Address"; description="Updated"; capacity=150; isActive=$true } $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
}

Test-Case "UC10 Student cannot create location -> 403" {
    $r = Invoke-Api POST "/api/locations" @{ name="Blocked"; address="X"; capacity=10 } $script:studentToken
    if ($r.Status -ne 403) { throw "Expected 403, got $($r.Status)" }
}

Test-Case "UC10 Staff delete location (no events)" {
    $r = Invoke-Api DELETE "/api/locations/$($script:testLocationId)" $null $script:staffToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status): $($r.Body)" }
}

# UC10 Organizers
Test-Case "UC10 GET organizers anonymous" {
    $r = Invoke-Api GET "/api/organizers"
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
}

Test-Case "UC10 Admin create organizer" {
    $r = Invoke-Api POST "/api/organizers" @{ name="Test Org"; contactEmail="org@test.com"; contactPhone="0901111222"; description="Test" } $script:adminToken
    if ($r.Status -ne 201) { throw "Expected 201, got $($r.Status): $($r.Body)" }
    $script:testOrganizerId = ($r.Body | ConvertFrom-Json).id
}

Test-Case "UC10 Admin delete organizer (no events)" {
    $r = Invoke-Api DELETE "/api/organizers/$($script:testOrganizerId)" $null $script:adminToken
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
}

# Content Negotiation
Test-Case "Content Negotiation JSON" {
    $r = Invoke-Api GET "/api/locations" $null $null "application/json"
    if ($r.Status -ne 200) { throw "Expected 200" }
    if ($r.Body -notmatch '^\s*\[') { throw "Expected JSON array" }
}

Test-Case "Content Negotiation XML" {
    $r = Invoke-Api GET "/api/locations" $null $null "application/xml"
    if ($r.Status -ne 200) { throw "Expected 200, got $($r.Status)" }
    if ($r.Body -notmatch '<') { throw "Expected XML response" }
}

Test-Case "Content Negotiation CSV -> 406" {
    $r = Invoke-Api GET "/api/locations" $null $null "text/csv"
    if ($r.Status -ne 406) { throw "Expected 406, got $($r.Status)" }
}

Write-Host "`n=== SUMMARY ===" -ForegroundColor Cyan
Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
if ($failed -gt 0) {
    Write-Host "`nFailed tests:" -ForegroundColor Red
    $results | Where-Object { $_.Result -like "FAIL*" } | ForEach-Object { Write-Host "  - $($_.Test): $($_.Result)" }
}
exit $failed
