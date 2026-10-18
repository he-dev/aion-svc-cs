
return # Prevents accidental execution of the entire script.


winget install jqlang.jq --scope user


$AionApi="http://localhost:5049/api"
$Profile="hallo"


curl.exe --request GET $AionApi/profiles/$Profile/schedules --show-headers | jq
curl.exe --request GET $AionApi/profiles/$Profile/schedules | ConvertFrom-Json | ConvertTo-Json
Invoke-WebRequest -Uri $AionApi/profiles/$Profile/schedules -Method Get



curl.exe --request GET $AionApi/profiles/$Profile/schedules --silent | jq

curl.exe --request POST $AionApi/profiles/$Profile/workflows/hallo-npc-lite:start-now `
    --silent `
    --header "Content-Type: application/json" `
    --data '{"steps": [0]}' `
    | jq