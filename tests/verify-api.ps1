param([string]$BaseUrl='http://localhost:5188')
$ErrorActionPreference='Stop'
$checks=0
function Verify($condition,[string]$message){if(-not $condition){throw $message};$script:checks++;Write-Output "PASS $message"}
foreach($path in @('/health','/_framework/blazor.web.js','/_content/Lumen.Charts.Blazor/lumen.css','/_content/Lumen.Charts.Blazor/lumen.js')){
 $r=Invoke-WebRequest "$BaseUrl$path" -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 200) "Asset/health $path"
}
$r=Invoke-WebRequest "$BaseUrl/sports" -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and ([regex]::Matches($r.Content,'class="lumen-chart lumen-fit"')).Count -eq 12 -and $r.Content.Contains('not real training data')) 'The Sports & performance page answers 200 and prerenders its twelve simulated charts, each set to fit its card'
$types=Invoke-RestMethod "$BaseUrl/api/charts/types"
Verify ($types.Count -eq 18 -and $types -contains 'Gauge' -and $types -contains 'Ring') 'Eighteen chart types, gauge and ring among them'
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
 @{name='Ohlc';marker="x1='59'";body='{"title":"Prices","kind":"Ohlc","series":[{"name":"ACME","points":[{"x":0,"open":10,"high":12,"low":9,"close":11},{"x":1,"open":11,"high":13,"low":10,"close":10.4}]}]}'},
 @{name='Band';marker="fill-opacity='.16'";body='{"title":"Forecast","kind":"Band","series":[{"name":"Demand","points":[{"x":0,"y":10,"low":8,"high":12},{"x":1,"y":12,"low":9,"high":15}]}]}'},
 @{name='Histogram';marker='equal-width bins';body='{"title":"Latency","kind":"Histogram","bins":5,"series":[{"name":"Requests","points":[{"x":0,"y":1},{"x":1,"y":2},{"x":2,"y":2},{"x":3,"y":3},{"x":4,"y":5},{"x":5,"y":8}]}]}'},
 @{name='Box';marker='median';body='{"title":"Spread","kind":"Box","series":[{"name":"Europe","points":[{"x":0,"y":1},{"x":1,"y":2},{"x":2,"y":3},{"x":3,"y":4},{"x":4,"y":40}]}]}'},
 @{name='Violin';marker='observations, median';body='{"title":"Shape","kind":"Violin","series":[{"name":"Europe","points":[{"x":0,"y":1},{"x":1,"y":2},{"x":2,"y":2},{"x":3,"y":3},{"x":4,"y":5},{"x":5,"y":8},{"x":6,"y":13},{"x":7,"y":21}]}]}'},
 @{name='Gauge';marker="class='lumen-gauge-value'";body='{"title":"Recovery","kind":"Gauge","yLabel":"%","gaugeSweep":270,"yZones":{"zones":[{"name":"Low","upper":33,"color":"#DD4B45"},{"name":"Moderate","upper":66,"color":"#A88200"},{"name":"Good","upper":"Infinity","color":"#2E9B58"}]},"annotations":[{"axis":"Y","from":60,"label":"Average"}],"series":[{"name":"Recovery","points":[{"x":0,"y":72,"label":"Recovery"}]}]}'},
 @{name='Ring';marker="class='lumen-ring-progress'";body='{"title":"Activity","kind":"Ring","series":[{"name":"Move","goal":600,"points":[{"x":0,"y":540,"label":"kcal"}]},{"name":"Exercise","goal":30,"points":[{"x":0,"y":47,"label":"min"}]},{"name":"Stand","goal":12,"points":[{"x":0,"y":9,"label":"h"}]}]}'})
foreach($family in $families){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $family.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 200 -and ([xml]$r.Content).DocumentElement.LocalName -eq 'svg') "$($family.name) SVG response"
 Verify ($r.Content.Contains($family.marker)) "$($family.name) rendered its own geometry"
}
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $families[0].body
Verify ($r.Content.StartsWith('Series,X,Y,Label,Size,Open,High,Low,Close')) 'Candlestick CSV exports prices'
# A gauge's score and a ring's progress are named with their zone, unit and goal, a gauge's target is a tick across its arc, and a
# ring past its goal runs on over itself, its leading end shadowed.
$gauge=$families|Where-Object{$_.name -eq 'Gauge'};$ring=$families|Where-Object{$_.name -eq 'Ring'}
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $gauge.body
Verify ($r.Content.Contains("aria-label='Recovery: 72 %, Good'") -and $r.Content.Contains("aria-label='Good: above 66'") -and $r.Content.Contains("class='lumen-gauge-target'") -and $r.Content.Contains('>Average: 60<')) 'A gauge posted as JSON names its score and zone and marks its target'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $ring.body
Verify ($r.Content.Contains("aria-label='Move: 540 of 600 kcal, 90 %'") -and $r.Content.Contains("aria-label='Exercise: 47 of 30 min, 157 %'") -and ([regex]::Matches($r.Content,"class='lumen-ring-shadow'")).Count -eq 3 -and $r.Content.Contains('>Stand: 9 of 12 h<')) 'Rings posted as JSON name each value and goal, and only the ring past its goal casts a shadow'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $ring.body
Verify ($r.Content.StartsWith('Series,X,Y,Label,Size,Goal') -and $r.Content.Contains('"Move",0,540,"kcal",1,600')) 'Ring CSV carries each ring''s goal'
foreach($bad in @(@{body=$gauge.body.Replace('{"x":0,"y":72,"label":"Recovery"}','{"x":0,"y":72},{"x":1,"y":40}');reason='exactly one point';name='A gauge of two points'},
  @{body=$gauge.body.Replace('"gaugeSweep":270','"gaugeSweep":120');reason='between 180';name='A gauge sweep of 120 degrees'},
  @{body=$ring.body.Replace('"goal":600','"goal":0');reason='positive';name='A ring goal of zero'},
  @{body=$ring.body.Replace('"kind":"Ring",','"kind":"Ring","yZones":{"zones":[{"name":"All","upper":"Infinity"}]},');reason='no zones';name='Zones on rings'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
$summary='{"title":"Warehouse","kind":"Box","series":[{"name":"Asia","points":[],"summary":{"q1":205,"median":228,"q3":252,"lowerWhisker":160,"upperWhisker":318,"outliers":[352,371]}}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $summary -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and ([xml]$r.Content).DocumentElement.LocalName -eq 'svg') 'A precomputed box summary posted as JSON renders'
Verify ($r.Content.Contains('Asia (supplied summary): median 228, quartiles 205 to 252, whiskers 160 to 318, 2 outliers') -and $r.Content.Contains('>Asia (summary)<') -and -not $r.Content.Contains('data-point=')) 'A supplied summary is drawn as given and names no point'
foreach($kind in @('Violin','Line')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $summary.Replace('"kind":"Box"','"kind":"'+$kind+'"') -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) "A summary on a $kind chart is rejected"
}
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $summary.Replace('"points":[]','"points":[{"x":0,"y":230}]') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A series with both points and a summary is rejected'
$compared='{"title":"Latency","kind":"Histogram","bins":4,"series":[{"name":"Before","points":[{"x":0,"y":1},{"x":1,"y":5},{"x":2,"y":12},{"x":3,"y":25}]},{"name":"After","points":[{"x":0,"y":2},{"x":1,"y":3},{"x":2,"y":4},{"x":3,"y":39}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $compared -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and ([xml]$r.Content).DocumentElement.LocalName -eq 'svg') 'A two-series histogram renders'
Verify ($r.Content.Contains('Before, 1 to 10.5: 2 observations') -and $r.Content.Contains('After, 1 to 10.5: 3 observations') -and $r.Content.Contains('>Before<') -and $r.Content.Contains('>After<') -and $r.Content.Contains('8 observations across 2 series in 4 shared equal-width bins')) 'A two-series histogram draws and names both series'
$five=$compared.Replace('"series":[','"series":[{"name":"A","points":[{"x":0,"y":1}]},{"name":"B","points":[{"x":0,"y":1}]},{"name":"C","points":[{"x":0,"y":1}]},')
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $five -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A histogram of five series is rejected'
foreach($bad in @('{"kind":"Candlestick","series":[{"name":"P","points":[{"x":0,"y":1}]}]}','{"kind":"Band","series":[{"name":"F","points":[{"x":0,"y":1,"low":5,"high":2}]}]}','{"kind":"Histogram","bins":0,"series":[{"name":"H","points":[{"x":0,"y":1}]}]}')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'Invalid family request rejected'
}
$timeSpec='{"title":"Time","kind":"Line","xAxis":"Time","series":[{"name":"Signal","points":[{"x":1767225600000,"y":3},{"x":1769904000000,"y":5},{"x":1772323200000,"y":4}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $timeSpec
Verify ($r.Content -match 'Feb 2026|Jan 2026') 'Time axis renders calendar labels'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $timeSpec
Verify ($r.Content.Contains('Series,X,XTime,Y,Label,Size') -and $r.Content.Contains('2026-01-01T00:00:00.000Z')) 'Time CSV adds ISO timestamps'
$zoned='{"title":"Local","kind":"Line","xAxis":"Time","timeZone":"America/New_York","series":[{"name":"Signal","points":[{"x":1767571200000,"y":1},{"x":1767592800000,"y":2},{"x":1767614400000,"y":3}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $zoned
Verify ($r.Content.Contains('4 Jan 2026 19:00')) 'A time axis reads its calendar in the requested zone'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Line","xAxis":"Time","timeZone":"Mars/Olympus","series":[{"name":"S","points":[{"x":1767571200000,"y":1}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'An unknown time zone is rejected'
$trading='{"title":"Trading","kind":"Line","xAxis":"Time","skipWeekends":true,"series":[{"name":"Close","points":[{"x":1767571200000,"y":1},{"x":1767657600000,"y":2},{"x":1767744000000,"y":3},{"x":1767830400000,"y":4},{"x":1767916800000,"y":5},{"x":1768176000000,"y":6},{"x":1768262400000,"y":7},{"x":1768348800000,"y":8},{"x":1768435200000,"y":9},{"x":1768521600000,"y":10}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $trading
Verify ($r.Content.Contains('>5 Jan<') -and -not $r.Content.Contains('>10 Jan<') -and -not $r.Content.Contains('>11 Jan<')) 'A trading axis labels no weekend'
$holiday=$trading.Replace('"skipWeekends":true','"skipWeekends":true,"timeSkips":[{"from":1767744000000,"to":1767830400000}]')
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $holiday
Verify (-not $r.Content.Contains('>7 Jan<')) 'A skipped holiday leaves the axis'
foreach($bad in @('{"kind":"Line","skipWeekends":true,"series":[{"name":"S","points":[{"x":1,"y":1}]}]}','{"kind":"Line","xAxis":"Time","timeSkips":[{"from":1767744000000,"to":1767744000000}],"series":[{"name":"S","points":[{"x":1767571200000,"y":1}]}]}')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'An impossible skipped span is rejected'
}
$trend='{"title":"Fit","kind":"Scatter","series":[{"name":"Accounts","trend":true,"points":[{"x":1,"y":3},{"x":2,"y":5},{"x":3,"y":7},{"x":4,"y":9}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $trend
Verify ($r.Content.Contains("class='lumen-trend'") -and $r.Content.Contains('R squared 1.00') -and $r.Content.Contains('rising')) 'A scatter series carries its least-squares line'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $trend.Replace('"trend":true','"trend":false')
Verify (-not $r.Content.Contains('lumen-trend')) 'A series that asks for no trend draws none'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $trend.Replace('"kind":"Scatter"','"kind":"Column"') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A trend line on a category chart is rejected'
$logSpec='{"title":"Log","kind":"Scatter","yAxis":"Log","series":[{"name":"Load","points":[{"x":1,"y":2},{"x":2,"y":200},{"x":3,"y":20000}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $logSpec
Verify (([xml]$r.Content).DocumentElement.LocalName -eq 'svg') 'Log axis SVG'
foreach($bad in @('{"kind":"Line","yAxis":"Log","series":[{"name":"S","points":[{"x":1,"y":0}]}]}','{"kind":"Column","xAxis":"Time","series":[{"name":"S","points":[{"x":1,"y":1}]}]}','{"kind":"Line","yAxis":"Time","series":[{"name":"S","points":[{"x":1,"y":1}]}]}')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'Invalid axis request rejected'
}
$paired='{"title":"Two units","kind":"Line","y2Label":"Rate","series":[{"name":"Accounts","points":[{"x":0,"y":100},{"x":1,"y":500}]},{"name":"Rate","secondary":true,"points":[{"x":0,"y":2},{"x":1,"y":4}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $paired
Verify ($r.Content.Contains('Rate: 1, 4') -and $r.Content.Contains('rotate(90')) 'A secondary series is measured and named on the right'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"StackedColumn","series":[{"name":"A","points":[{"x":0,"y":1}]},{"name":"B","secondary":true,"points":[{"x":0,"y":2}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A secondary axis on a stacked chart is rejected'
$duration='{"title":"Pace","kind":"Line","yFormat":"Duration","series":[{"name":"Pace","points":[{"x":0,"y":290},{"x":1,"y":305},{"x":2,"y":320}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $duration
Verify ($r.Content.Contains('>4:50<') -and $r.Content.Contains('>5:20<') -and $r.Content.Contains('Pace: 1, 5:05')) 'A duration axis posted as JSON reads m:ss'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $duration
Verify ($r.Content.Contains('"Pace",1,305,')) 'CSV keeps durations in seconds'
$curve='{"title":"Power","kind":"Line","xAxis":"Log","xFormat":"Duration","series":[{"name":"Power","points":[{"x":1,"y":900},{"x":60,"y":420},{"x":1200,"y":290},{"x":3600,"y":260}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $curve
Verify ($r.Content.Contains('>1s<') -and $r.Content.Contains('>1m<') -and $r.Content.Contains('>1h<') -and $r.Content.Contains('Power: 20m, 290')) 'A log duration axis reads 1s, 1m and 1h'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $duration.Replace('"yFormat"','"yReversed":true,"yFormat"') -SkipHttpErrorCheck
$fast=[double][regex]::Match($r.Content,"y='([\d.]+)'[^>]*>4:50<").Groups[1].Value;$slow=[double][regex]::Match($r.Content,"y='([\d.]+)'[^>]*>5:20<").Groups[1].Value
Verify ($r.StatusCode -eq 200 -and $fast -gt 0 -and $fast -lt $slow) 'A reversed axis renders with the faster pace on top'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Column","yFormat":"Compact","series":[{"name":"Views","points":[{"x":0,"y":1500,"label":"A"},{"x":1,"y":2400000,"label":"B"}]}]}'
Verify ($r.Content.Contains('>2M<') -and $r.Content.Contains('Views: A, 1.5k')) 'Compact numbers read 1.5k and 2M'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Column","yReversed":true,"series":[{"name":"S","points":[{"x":0,"y":1}]}]}' -SkipHttpErrorCheck
# A problem response is application/problem+json, which PowerShell hands back as bytes, so the text is read from RawContent.
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('zero baseline')) 'A reversed column chart is rejected for its zero baseline'
foreach($bad in @('{"kind":"Line","xAxis":"Time","xFormat":"Duration","series":[{"name":"S","points":[{"x":1767225600000,"y":1}]}]}','{"kind":"Line","xAxis":"Time","xFormat":"Compact","series":[{"name":"S","points":[{"x":1767225600000,"y":1}]}]}','{"kind":"Line","yFormat":"Pace","series":[{"name":"S","points":[{"x":0,"y":1}]}]}')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'A format on a time axis, or an unknown format, is rejected'
}
$annotated='{"title":"Target","kind":"Line","annotations":[{"axis":"Y","from":25,"label":"Target"},{"axis":"X","from":1,"to":2,"label":"Window"}],"series":[{"name":"S","points":[{"x":0,"y":10},{"x":1,"y":30},{"x":2,"y":20},{"x":3,"y":40}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $annotated
Verify ($r.Content.Contains('Target: 25') -and $r.Content.Contains('Window: 1 to 2')) 'Annotations in the request are drawn and named'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"title":"Target","kind":"Bar","annotations":[{"axis":"Y","from":25,"label":"Target"}],"series":[{"name":"S","points":[{"x":0,"y":10,"label":"A"},{"x":1,"y":40,"label":"B"}]}]}'
# The value axis runs along X from 0 at 160 to 40 at 870, so 25 sits at 603.75, and the plot spans 78 to 344 down the page.
$line=([xml]$r.Content).SelectNodes('//*[local-name()="g"][@aria-label="Target: 25"]/*[local-name()="line"]')|Select-Object -Last 1
Verify ($line.x1 -eq '603.75' -and $line.x2 -eq '603.75' -and $line.y1 -eq '78' -and $line.y2 -eq '344') 'A value annotation on a horizontal bar chart stands upright at its value'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Donut","annotations":[{"axis":"Y","from":1}],"series":[{"name":"S","points":[{"x":0,"y":1}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'An annotation on a chart without axes is rejected'
$branded='{"title":"Brand","kind":"Line","style":{"background":"#F6F3EE","series":["#1D4E89"],"fontFamily":"Georgia,serif"},"series":[{"name":"S","points":[{"x":0,"y":1},{"x":1,"y":2}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $branded
Verify ($r.Content.Contains('background:#F6F3EE') -and $r.Content.Contains("fill='#1D4E89'") -and $r.Content.Contains('font-family:Georgia,serif')) 'A style in the request brands the SVG'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Line","style":{"fontFamily":"Arial;background:url(x)"},"series":[{"name":"S","points":[{"x":0,"y":1}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A style that could escape the markup is rejected'
$zoned='{"title":"Effort","kind":"Line","yZones":{"zones":[{"name":"Easy","upper":120},{"name":"Steady","upper":140},{"name":"Hard","upper":"Infinity"}]},"series":[{"name":"Heart rate","zones":{"zones":[{"name":"Easy","upper":120},{"name":"Steady","upper":140},{"name":"Hard","upper":"Infinity"}]},"points":[{"x":0,"y":110},{"x":1,"y":150},{"x":2,"y":130}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $zoned
$strokes=@([regex]::Matches($r.Content,"fill='none' stroke='(#[0-9A-F]{6})' stroke-width='1.6' stroke-linejoin='round' stroke-linecap='round' vector-effect='non-scaling-stroke'")|ForEach-Object{$_.Groups[1].Value}|Sort-Object -Unique)
Verify ($strokes.Count -ge 3 -and $r.Content.Contains('Heart rate: 1, 150, Hard')) 'A zone-coloured line posted as JSON changes stroke colour at its bounds and names each zone'
Verify ($r.Content.Contains('>Easy: up to 120<') -and $r.Content.Contains('>Steady: 120 to 140<') -and $r.Content.Contains('>Hard: above 140<')) 'Zone bands render their names and ranges'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $zoned.Replace('"kind":"Line",','"kind":"Line","style":{"finish":"Classic"},')
$classic=@([regex]::Matches($r.Content,"fill='none' stroke='(#[0-9A-F]{6})' stroke-width='2.5' stroke-linejoin='round'/>")|ForEach-Object{$_.Groups[1].Value}|Sort-Object -Unique)
Verify ($r.StatusCode -eq 200 -and $classic.Count -ge 3 -and -not $r.Content.Contains('vector-effect') -and -not $r.Content.Contains('lumen-marker') -and -not $r.Content.Contains("stroke-width='1.6'")) 'A classic finish posted as JSON draws the 2.5-pixel strokes and visible markers of 0.23.0'
$coloured='{"title":"Time in zone","kind":"Bar","yFormat":"Duration","series":[{"name":"Time","points":[{"x":0,"y":600,"label":"Easy","color":"#848484"},{"x":1,"y":1500,"label":"Steady","color":"#3F87D9"},{"x":2,"y":300,"label":"Hard","color":"#2E9B58"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $coloured
Verify ($r.Content.Contains("fill='#848484'") -and $r.Content.Contains("fill='#3F87D9'") -and $r.Content.Contains("fill='#2E9B58'") -and $r.Content.Contains('Time: Steady, 25:00')) 'Point colours posted as JSON colour their bars'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"StackedColumn","series":[{"name":"A","zones":{"zones":[{"name":"All","upper":"Infinity"}]},"points":[{"x":0,"y":1}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'Zones on a stacked column are rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Candlestick","series":[{"name":"P","points":[{"x":0,"open":10,"high":12,"low":9,"close":11,"color":"#123456"}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A point colour on a candlestick is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $zoned.Replace('"upper":140','"upper":100') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A zone scale whose bounds do not rise is a bad request, not a server error'
$mixed='{"title":"Training","kind":"Line","y2Label":"Form","series":[{"name":"Fitness","projectedFrom":2,"points":[{"x":0,"y":40},{"x":1,"y":42},{"x":2,"y":45},{"x":3,"y":44}]},{"name":"Stress","kind":"Column","points":[{"x":0,"y":80},{"x":1,"y":0},{"x":2,"y":120},{"x":3,"y":60}]},{"name":"Form","kind":"Area","secondary":true,"points":[{"x":0,"y":-5},{"x":1,"y":3},{"x":2,"y":-1},{"x":3,"y":2}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $mixed
$xml=[xml]$r.Content
$bars=@($xml.SelectNodes('//*[local-name()="g"][starts-with(@aria-label,"Stress: ")]/*[local-name()="rect"]'))
$strokes=@($xml.SelectNodes('//*[local-name()="path"][@fill="none"][@stroke-width="1.6"][@vector-effect="non-scaling-stroke"]'))
Verify ($r.StatusCode -eq 200 -and $bars.Count -eq 4 -and $strokes.Count -ge 2 -and $r.Content.Contains("fill-opacity='.12'")) 'A mixed chart posted as JSON renders its lines, its columns and its area'
Verify (@($strokes|Where-Object{$_.GetAttribute('stroke-dasharray') -eq '4.4 5.6'}).Count -ge 1 -and $r.Content.Contains('Fitness: 3, 44, projected') -and -not $r.Content.Contains('Fitness: 1, 42, projected')) 'A projected series posted as JSON renders a dashed stroke and names its projected marks'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $mixed.Replace('"kind":"Line",','"kind":"Line","yAxis":"Log",').Replace('{"x":1,"y":0}','{"x":1,"y":10}') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('logarithmic')) 'A column series on a log axis is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Donut","series":[{"name":"S","kind":"Line","points":[{"x":0,"y":1},{"x":1,"y":2}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('own kind')) 'A series kind on a donut is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Line","series":[{"name":"S","kind":"Column","projectedFrom":1,"points":[{"x":0,"y":1},{"x":1,"y":2}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('lines or areas')) 'A projection on a column series is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body '{"kind":"Line","series":[{"name":"Load","points":[{"x":0,"y":5}]},{"name":"Target","kind":"Band","points":[{"x":0,"y":5,"low":4,"high":6}]}]}'
Verify ($r.Content.StartsWith('Series,X,Y,Label,Size,Low,High') -and $r.Content.Contains('"Load",0,5,"",1,,') -and $r.Content.Contains('"Target",0,5,"",1,4,6')) 'CSV carries the edges of a band drawn on a line chart'
$market='{"title":"Market","kind":"Candlestick","xAxis":"Time","skipWeekends":true,"panes":[{"label":"Volume","weight":0.4,"yFormat":"Compact"}],"series":[{"name":"ACME","points":[{"x":1772409600000,"open":10,"high":12,"low":9,"close":11},{"x":1772496000000,"open":11,"high":13,"low":10,"close":10.4},{"x":1772582400000,"open":10.4,"high":11,"low":9.8,"close":10.9}]},{"name":"Volume","kind":"Column","pane":1,"points":[{"x":1772409600000,"y":1500000},{"x":1772496000000,"y":2100000},{"x":1772582400000,"y":1800000}]},{"name":"Average","kind":"Line","points":[{"x":1772409600000,"y":11},{"x":1772496000000,"y":10.7},{"x":1772582400000,"y":10.77}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $market -SkipHttpErrorCheck
$clips=@(([xml]$r.Content).DocumentElement.ChildNodes|Where-Object{$_.LocalName -eq 'svg'})
Verify ($r.StatusCode -eq 200 -and $clips.Count -eq 2 -and @($clips[0].SelectNodes('.//*[contains(@aria-label,": open ")]')).Count -eq 3 -and @($clips[1].SelectNodes('.//*[starts-with(@aria-label,"Volume: ")]/*[local-name()="rect"]')).Count -eq 3 -and $r.Content.Contains('Volume: 3 Mar 2026, 2.1M') -and $r.Content.Contains('Average: 4 Mar 2026, 10.77')) 'A candlestick with a volume pane posted as JSON draws its candles and average above and its volume beneath'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $market.Replace('"pane":1','"pane":2') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('Panes[k - 1]')) 'A series in a pane no ChartPane describes is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Column","panes":[{"label":"Below"}],"series":[{"name":"A","points":[{"x":0,"y":1}]},{"name":"B","pane":1,"points":[{"x":0,"y":2}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('continuous X axis')) 'Panes on a column chart are rejected'
$finish='{"title":"Finish","kind":"Area","style":{"gridlines":"Dotted"},"series":[{"name":"Climb","curve":"Smooth","fill":"Fade","markers":"None","points":[{"x":0,"y":10},{"x":1,"y":30},{"x":2,"y":20},{"x":3,"y":40}]},{"name":"Heart rate","kind":"Line","curve":"Smooth","strokeWidth":3,"highlightLast":true,"gradient":[{"value":10,"color":"#3F87D9"},{"value":40,"color":"#DD4B45"}],"points":[{"x":0,"y":12},{"x":1,"y":25},{"x":2,"y":38},{"x":3,"y":30}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $finish -SkipHttpErrorCheck
$gradients=@(([xml]$r.Content).SelectNodes('//*[local-name()="linearGradient"]'))
$paints=@([regex]::Matches($r.Content,"url\(([^)]*)\)")|ForEach-Object{$_.Groups[1].Value}|Sort-Object -Unique)
Verify ($r.StatusCode -eq 200 -and $gradients.Count -eq 2 -and $r.Content.Contains(' C') -and $r.Content.Contains("stroke-dasharray='1 3'") -and $r.Content.Contains("r='10'")) 'A smooth, faded, gradient chart posted as JSON renders its gradients'
Verify ($paints.Count -eq 2 -and @($paints|Where-Object{-not $_.StartsWith('#')}).Count -eq 0 -and @($gradients|Where-Object{$paints -notcontains ('#'+$_.id)}).Count -eq 0 -and -not $r.Content.Contains('href') -and ([regex]::Matches($r.Content,'http://')).Count -eq 1) 'A chart with gradients stays self-contained'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $finish.Replace('"strokeWidth":3,','"strokeWidth":3,"zones":{"zones":[{"name":"Low","upper":20},{"name":"High","upper":"Infinity"}]},') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('one or the other')) 'A gradient together with zones is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $finish.Replace('"strokeWidth":3','"strokeWidth":20') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('0.5 and 12')) 'A stroke width of 20 is rejected'
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
