import json
import csv
import requests

SWAGGER_FILE = 'swagger.json'
# Default to the local HTTPS host used during debugging. Can be overridden by
# setting the BASE_URL environment variable when running the script.
import os
BASE = os.environ.get('BASE_URL', 'https://localhost:44356')
CSV_OUT = 'api-execution-results.csv'
spec = None
# swagger.json may be emitted with UTF-16/UTF-8 BOM depending on the host; try common encodings
for enc in ('utf-8', 'utf-8-sig', 'utf-16', 'utf-16le', 'utf-16be'):
	try:
		with open(SWAGGER_FILE, 'r', encoding=enc) as f:
			spec = json.load(f)
		print(f'Loaded swagger.json using encoding: {enc}')
		break
	except Exception:
		spec = None
		continue
if spec is None:
	raise RuntimeError(f"Failed to load {SWAGGER_FILE} with supported encodings")

ops = []
for path, pinfo in spec.get('paths', {}).items():
	for method, op in pinfo.items():
		ops.append((method.upper(), path, op))

# login to get token (positive scenario)
token = None
try:
	r = requests.post(BASE + '/api/auth/login', json={'username': 'admin', 'password': 'password'}, timeout=10, verify=False)
	r.raise_for_status()
	j = r.json()
	token = j.get('token') or j.get('access_token') or j.get('tokenValue')
	print('Obtained token')
except Exception as e:
	print('Login failed (positive auth):', e)

rows = []
for method, path, op in ops:
	url = BASE + path
	headers = {'Accept': 'application/json'}
	data = None

	# Prepare simple JSON body for POST/PUT/PATCH
	if method in ('POST', 'PUT', 'PATCH'):
		rb = op.get('requestBody', {}).get('content', {})
		if 'application/json' in rb:
			schema = rb['application/json'].get('schema', {})
			# If schema references LoginModel, use login payload
			ref = schema.get('$ref')
			if ref and 'LoginModel' in ref:
				data = {'username': 'admin', 'password': 'password'}
			elif schema.get('type') == 'string':
				data = 'sample'
			elif schema.get('type') == 'object':
				props = schema.get('properties', {})
				data = {}
				for k, v in props.items():
					t = v.get('type')
					if t == 'string':
						data[k] = 'sample'
					elif t == 'integer':
						data[k] = 1
					elif t == 'number':
						data[k] = 1.0
					elif t == 'boolean':
						data[k] = True
					else:
						data[k] = None
			else:
				data = {}
		else:
			data = {}
		headers['Content-Type'] = 'application/json'

	# Execute negative scenario: without token (or login itself)
	try:
		resp = requests.request(method, url, headers=headers, json=data, timeout=15, verify=False)
		status = resp.status_code
		body = resp.text
	except Exception as ex:
		status = 'ERR'
		body = str(ex)

	rows.append({'method': method, 'path': path, 'request': json.dumps(data, ensure_ascii=False) if data is not None else '', 'status': status, 'response': body})

	# Execute positive scenario with token if we obtained one and this is not the login endpoint
	if token and not path.lower().startswith('/api/auth/login'):
		headers_with_token = dict(headers)
		headers_with_token['Authorization'] = 'Bearer ' + token
		try:
			resp = requests.request(method, url, headers=headers_with_token, json=data, timeout=15, verify=False)
			status = resp.status_code
			body = resp.text
		except Exception as ex:
			status = 'ERR'
			body = str(ex)

		rows.append({'method': method + ' (auth)', 'path': path, 'request': json.dumps(data, ensure_ascii=False) if data is not None else '', 'status': status, 'response': body})

# Write CSV
with open(CSV_OUT, 'w', newline='', encoding='utf-8') as f:
	writer = csv.DictWriter(f, fieldnames=['method', 'path', 'request', 'status', 'response'])
	writer.writeheader()
	for r in rows:
		writer.writerow(r)

print('Wrote', CSV_OUT)
