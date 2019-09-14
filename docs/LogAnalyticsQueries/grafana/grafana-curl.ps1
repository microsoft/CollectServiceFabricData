$apiToken = ""
$dashboard = "https://jagilber.grafana.net/api/dashboards/uid/xxxxxxxxx"
$result = curl.exe -H "Authorization: Bearer $apiToken" $dashboard
$global:grafanaSchema = $result | convertfrom-json | convertto-json -depth 99
$global:grafanaSchema
write-host "json saved in `$global:grafanaSchema"