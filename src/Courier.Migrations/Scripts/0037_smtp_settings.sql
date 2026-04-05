-- SMTP email settings (stored in system_settings key-value table)
INSERT INTO system_settings (key, value, description, updated_by) VALUES
    ('smtp.host', '', 'SMTP server hostname', 'system'),
    ('smtp.port', '587', 'SMTP server port', 'system'),
    ('smtp.use_ssl', 'true', 'Use SSL/TLS for SMTP connection', 'system'),
    ('smtp.username', '', 'SMTP authentication username', 'system'),
    ('smtp.password', '', 'SMTP authentication password', 'system'),
    ('smtp.from_address', '', 'Sender email address', 'system'),
    ('smtp.from_name', 'Courier', 'Sender display name', 'system')
ON CONFLICT (key) DO NOTHING;
