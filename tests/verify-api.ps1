param([string]$BaseUrl='http://localhost:5188')
$ErrorActionPreference='Stop'
$checks=0
function Verify($condition,[string]$message){if(-not $condition){throw $message};$script:checks++;Write-Output "PASS $message"}
foreach($path in @('/health','/_framework/blazor.web.js','/_content/Lumen.Charts.Blazor/lumen.css','/_content/Lumen.Charts.Blazor/lumen.js')){
 $r=Invoke-WebRequest "$BaseUrl$path" -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 200) "Asset/health $path"
}
$types=Invoke-RestMethod "$BaseUrl/api/charts/types"
Verify ($types.Count -eq 14) 'Fourteen chart types'
foreach($kind in @('Line','Area','Scatter','Bubble','Column','Bar','StackedColumn','Donut','Heatmap','Radar')){
 $spec=@{title='API test';kind=$kind;series=@(@{name='Sample';points=@(@{x=0;y=2;label='A'},@{x=1;y=4;label='B'},@{x=2;y=3;label='C'})})}
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body ($spec|ConvertTo-Json -Depth 10) -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 200 -and $r.Headers['Content-Type'] -like 'image/svg+xml*') "$kind SVG response"
 $xml=[xml]$r.Content
 Verify ($xml.DocumentElement.LocalName -eq 'svg') "$kind XML validity"
}
$payload='{"title":"CSV","kind":"Line","series":[{"name":"Source","points":[{"x":0,"y":1},{"x":1,"y":null}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $payload
Verify ($r.Content.Contains('Series,X,Y,Label,Size') -and $r.Content.Contains('"Source",1,,')) 'CSV retains missing observation'
$families=@(
 @{name='Candlestick';marker="rx='1'";body='{"title":"Prices","kind":"Candlestick","series":[{"name":"ACME","points":[{"x":0,"open":10,"high":12,"low":9,"close":11},{"x":1,"open":11,"high":13,"low":10,"close":10.4}]}]}'},
 @{name='Band';marker="fill-opacity='.16'";body='{"title":"Forecast","kind":"Band","series":[{"name":"Demand","points":[{"x":0,"y":10,"low":8,"high":12},{"x":1,"y":12,"low":9,"high":15}]}]}'},
 @{name='Histogram';marker='equal-width bins';body='{"title":"Latency","kind":"Histogram","bins":5,"series":[{"name":"Requests","points":[{"x":0,"y":1},{"x":1,"y":2},{"x":2,"y":2},{"x":3,"y":3},{"x":4,"y":5},{"x":5,"y":8}]}]}'},
 @{name='Box';marker='median';body='{"title":"Spread","kind":"Box","series":[{"name":"Europe","points":[{"x":0,"y":1},{"x":1,"y":2},{"x":2,"y":3},{"x":3,"y":4},{"x":4,"y":40}]}]}'})
foreach($family in $families){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $family.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 200 -and ([xml]$r.Content).DocumentElement.LocalName -eq 'svg') "$($family.name) SVG response"
 Verify ($r.Content.Contains($family.marker)) "$($family.name) rendered its own geometry"
}
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $families[0].body
Verify ($r.Content.StartsWith('Series,X,Y,Label,Size,Open,High,Low,Close')) 'Candlestick CSV exports prices'
foreach($bad in @('{"kind":"Candlestick","series":[{"name":"P","points":[{"x":0,"y":1}]}]}','{"kind":"Band","series":[{"name":"F","points":[{"x":0,"y":1,"low":5,"high":2}]}]}','{"kind":"Histogram","bins":0,"series":[{"name":"H","points":[{"x":0,"y":1}]}]}')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'Invalid family request rejected'
}
$timeSpec='{"title":"Time","kind":"Line","xAxis":"Time","series":[{"name":"Signal","points":[{"x":1767225600000,"y":3},{"x":1769904000000,"y":5},{"x":1772323200000,"y":4}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $timeSpec
Verify ($r.Content -match 'Feb 2026|Jan 2026') 'Time axis renders calendar labels'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $timeSpec
Verify ($r.Content.Contains('Series,X,XTime,Y,Label,Size') -and $r.Content.Contains('2026-01-01T00:00:00.000Z')) 'Time CSV adds ISO timestamps'
$logSpec='{"title":"Log","kind":"Scatter","yAxis":"Log","series":[{"name":"Load","points":[{"x":1,"y":2},{"x":2,"y":200},{"x":3,"y":20000}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $logSpec
Verify (([xml]$r.Content).DocumentElement.LocalName -eq 'svg') 'Log axis SVG'
foreach($bad in @('{"kind":"Line","yAxis":"Log","series":[{"name":"S","points":[{"x":1,"y":0}]}]}','{"kind":"Column","xAxis":"Time","series":[{"name":"S","points":[{"x":1,"y":1}]}]}','{"kind":"Line","yAxis":"Time","series":[{"name":"S","points":[{"x":1,"y":1}]}]}')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'Invalid axis request rejected'
}
$graph='{"nodes":[{"id":"a","label":"Start"},{"id":"b","label":"End"}],"edges":[{"source":"a","target":"b"}],"layout":"Layered"}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/svg" -Method Post -ContentType application/json -Body $graph
Verify ($r.StatusCode -eq 200 -and ([xml]$r.Content).DocumentElement.LocalName -eq 'svg') 'Graph SVG'
$positions=Invoke-RestMethod "$BaseUrl/api/charts/graph/layout" -Method Post -ContentType application/json -Body $graph
Verify ($positions.Count -eq 2 -and $positions[0].x -lt $positions[1].x) 'Layered coordinates'
$routes=Invoke-RestMethod "$BaseUrl/api/charts/graph/routes" -Method Post -ContentType application/json -Body $graph
Verify ($routes.Count -eq 1 -and $routes[0].points.Count -eq 2) 'Graph routes for an adjacent edge'
$spanning='{"nodes":[{"id":"a","label":"A"},{"id":"b","label":"B"},{"id":"c","label":"C"}],"edges":[{"source":"a","target":"b"},{"source":"b","target":"c"},{"source":"a","target":"c"}],"layout":"Layered"}'
$routes=Invoke-RestMethod "$BaseUrl/api/charts/graph/routes" -Method Post -ContentType application/json -Body $spanning
Verify ($routes[2].points.Count -eq 3) 'Long graph edges bend through each level'
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/svg" -Method Post -ContentType application/json -Body $spanning
Verify ($r.Content.Contains('data-position=') -and $r.Content.Contains('edge crossings')) 'Graph SVG exposes node handles and its crossing count'
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/routes" -Method Post -ContentType application/json -Body '{"nodes":[{"id":"a","label":"A"},{"id":"b","label":"B"}],"edges":[{"source":"a","target":"b"},{"source":"b","target":"a"}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'Cyclic route request rejected'
foreach($bad in @('{"kind":"Donut","series":[{"name":"Bad","points":[{"x":0,"y":-1}]}]}','{"width":99999}','{"series":null}','{"kind":"Bogus"}','{not json')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'Invalid request rejected'
}
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/layout" -Method Post -ContentType application/json -Body '{"nodes":[{"id":"a","label":"A"},{"id":"b","label":"B"}],"edges":[{"source":"a","target":"b"},{"source":"b","target":"a"}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'Cyclic layered graph rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/layout" -Method Post -ContentType application/json -Body '{"nodes":[{"id":"a","label":"A"}],"edges":[{"source":"a","target":"a"}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200) 'Layered self-loop accepted'
Write-Output "$checks HTTP checks passed."
