# Extended API tests - edge cases & BR-E-05
$base = "http://localhost:5181"
$passed = 0; $failed = 0

function Assert-Test($name, $cond, $detail = "") {
    if ($cond) { $script:passed++; Write-Host "[PASS] $name" -ForegroundColor Green }
    else { $script:failed++; Write-Host "[FAIL] $name - $detail" -ForegroundColor Red }
}

function Invoke-Api($method, $path, $body = $null, $token = $null, $accept = "application/json") {
    $headers = @{ Accept = $accept }
    if ($token) { $headers.Authorization = "Bearer $token" }
    $params = @{ Uri = "$base$path"; Method = $method; Headers = $headers }
    if ($body) { $params.ContentType = "application/json"; $params.Body = ($body | ConvertTo-Json) }
    try {
        $r = Invoke-WebRequest @params -UseBasicParsing
        return @{ Status = [int]$r.StatusCode; Body = $r.Content }
    } catch {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        return @{ Status = [int]$_.Exception.Response.StatusCode; Body = $reader.ReadToEnd() }
    }
}

function Get-Token($email, $password) {
    $r = Invoke-Api POST "/api/auth/login" @{ email = $email; password = $password }
    return ($r.Body | ConvertFrom-Json).token
}

$adminToken = Get-Token "admin@fpt.edu.vn" "Admin@123"
$staffToken = Get-Token "staff@fpt.edu.vn" "Staff@123"

Write-Host "=== Extended Tests ===" -ForegroundColor Cyan

# GET by id
$users = (Invoke-Api GET "/api/users?page=1&pageSize=1" $null $adminToken).Body | ConvertFrom-Json
$uid = $users.items[0].id
$r = Invoke-Api GET "/api/users/$uid" $null $adminToken
Assert-Test "GET /api/users/{id}" ($r.Status -eq 200)

$locs = (Invoke-Api GET "/api/locations").Body | ConvertFrom-Json
$lid = $locs[0].id
$r = Invoke-Api GET "/api/locations/$lid"
Assert-Test "GET /api/locations/{id}" ($r.Status -eq 200)

$orgs = (Invoke-Api GET "/api/organizers").Body | ConvertFrom-Json
$oid = $orgs[0].id
$r = Invoke-Api GET "/api/organizers/$oid"
Assert-Test "GET /api/organizers/{id}" ($r.Status -eq 200)

# Not found
$r = Invoke-Api GET "/api/users/999999" $null $adminToken
Assert-Test "GET /api/users/{id} not found -> 404" ($r.Status -eq 404)

$r = Invoke-Api GET "/api/locations/999999"
Assert-Test "GET /api/locations/{id} not found -> 404" ($r.Status -eq 404)

# Invalid role on create user
$r = Invoke-Api POST "/api/users" @{ email="badrole@test.com"; password="X@123"; fullName="X"; role="SuperAdmin" } $adminToken
Assert-Test "Create user invalid role -> 400" ($r.Status -eq 400)

# Duplicate email on admin create
$r = Invoke-Api POST "/api/users" @{ email="admin@fpt.edu.vn"; password="X@123"; fullName="Dup"; role="Staff" } $adminToken
Assert-Test "Create user duplicate email -> 409" ($r.Status -eq 409)

# XML account/me
$r = Invoke-Api GET "/api/account/me" $null $adminToken "application/xml"
Assert-Test "XML /api/account/me" ($r.Status -eq 200 -and $r.Body -match "UserDto")

# BR-E-05: delete location referenced by event
$createLoc = Invoke-Api POST "/api/locations" @{ name="BR-E-05 Loc"; address="Test"; capacity=50 } $staffToken
$locId = ($createLoc.Body | ConvertFrom-Json).id
sqlcmd -S localhost -U sa -P 123 -d EventRegistrationDb -C -Q "DECLARE @adminId INT=(SELECT TOP 1 Id FROM Users WHERE Email='admin@fpt.edu.vn'); DECLARE @orgId INT=(SELECT TOP 1 Id FROM Organizers); INSERT INTO Events (Title,StartTime,EndTime,RegistrationDeadline,Capacity,Status,LocationId,OrganizerId,CreatedById,CreatedAt) VALUES ('Test Event BR-E-05',DATEADD(day,1,GETUTCDATE()),DATEADD(day,1,DATEADD(hour,2,GETUTCDATE())),GETUTCDATE(),10,'Draft',$locId,@orgId,@adminId,GETUTCDATE());" | Out-Null
$r = Invoke-Api DELETE "/api/locations/$locId" $null $staffToken
Assert-Test "BR-E-05 Delete location with event -> 409" ($r.Status -eq 409)
sqlcmd -S localhost -U sa -P 123 -d EventRegistrationDb -C -Q "DELETE FROM Events WHERE Title='Test Event BR-E-05';" | Out-Null
Invoke-Api DELETE "/api/locations/$locId" $null $staffToken | Out-Null

Write-Host "`nPassed: $passed | Failed: $failed" -ForegroundColor Cyan
exit $failed
